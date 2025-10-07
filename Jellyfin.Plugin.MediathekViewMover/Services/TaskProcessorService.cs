using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewMover.Models;
using Jellyfin.Plugin.MediathekViewMover.Services.Interfaces;
using MediaBrowser.Controller;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.Services
{
    /// <summary>
    /// Service zur Verarbeitung von MoverTasks.
    /// </summary>
    public class TaskProcessorService
    {
        private readonly ILogger<TaskProcessorService> _logger;
        private readonly MediaConversionService _mediaConverter;
        private readonly IFileInfoService _fileInfoService;
        private readonly LanguageService _languageService;
        private readonly IAudioDescriptionService _audioDescriptionService;
        private readonly string _tempDirectory;
        private readonly CultureInfo _defaultCulture;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskProcessorService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        /// <param name="mediaConverter">Service für Medienkonvertierung.</param>
        /// <param name="fileInfoService">Service für Dateioperationen.</param>
        /// <param name="languageService">Service für Spracherkennung.</param>
        /// <param name="audioDescriptionService">Service für Audiodeskription.</param>
        /// <param name="configPaths">Server Configs Paths.</param>
        /// <param name="mediaEncoder">Media Encoder.</param>
        public TaskProcessorService(
            ILogger<TaskProcessorService> logger,
            MediaConversionService mediaConverter,
            IFileInfoService fileInfoService,
            LanguageService languageService,
            IAudioDescriptionService audioDescriptionService,
            IServerApplicationPaths configPaths,
            IMediaEncoder mediaEncoder)
        {
            _logger = logger;
            _mediaConverter = mediaConverter;
            _fileInfoService = fileInfoService;
            _languageService = languageService;
            _audioDescriptionService = audioDescriptionService;
            _defaultCulture = CultureInfo.GetCultureInfo("de");
            _tempDirectory = Path.Combine(configPaths.TempDirectory, "MediathekViewMover");
        }

        /// <summary>
        /// Verarbeitet einen einzelnen MoverTask.
        /// </summary>
        /// <param name="task">Der zu verarbeitende Task.</param>
        /// <param name="cancellationToken">Token für Abbruch der Operation.</param>
        /// <param name="progress">Progress Reporter.</param>
        /// <returns>Task für die asynchrone Operation.</returns>
        public async Task ProcessTaskAsync(MoverTask task, CancellationToken cancellationToken, IProgress<double> progress)
        {
            try
            {
                _logger.LogInformation("Verarbeite Task für: {Title}", task.Title);

                if (!Directory.Exists(task.SourceShowFolder))
                {
                    _logger.LogError("Quellordner nicht gefunden: {Path}", task.SourceShowFolder);
                    return;
                }

                var files = Directory.GetFiles(task.SourceShowFolder, "*.*", SearchOption.AllDirectories);
                var episodeGroups = GroupFilesByEpisode(files).ToList();
                var totalGroups = episodeGroups.Count;
                var processedGroups = 0;
                _fileInfoService.InitializeFileWatchers(files);
                // Warte 10 Sekunden, um festzustellen ob Dateien gerade in verwendung sind
                await Task.Delay(10000, cancellationToken).ConfigureAwait(false);

                foreach (var group in episodeGroups)
                {
                    var items = group.ToList();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (group.Key.Season == null || group.Key.Episode == null)
                    {
                        _logger.LogInformation("Überspringe Dateien ohne erkennbare Staffel/Episode");
                        processedGroups++;
                        progress.Report((double)processedGroups / totalGroups * 100);
                        continue;
                    }

                    if (items.Count < task.MinCount)
                    {
                        _logger.LogInformation("Folge hat zu wenige Versionen: S{Season:D2}E{Episode:D2} ({Count})", group.Key.Season, group.Key.Episode, items.Count);
                    }

                    var episodeSeason = (group.Key.Season.Value, group.Key.Episode.Value);
                    var seasonFolder = Path.Combine(task.TargetShowFolder, $"Staffel {group.Key.Season}");
                    try
                    {
                        await ProcessEpisodeGroupAsync(episodeSeason, items, seasonFolder, task.MinCount, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        throw;
                    }

                    processedGroups++;
                    progress.Report((double)processedGroups / totalGroups * 100);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Keine Berechtigung für den Zugriff auf Ordner: {Path}", task.SourceShowFolder);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Verarbeitung von Task: {Title}", task.Title);
                throw;
            }
        }

        private IEnumerable<IGrouping<(int? Season, int? Episode), FileInput>> GroupFilesByEpisode(IEnumerable<string> files)
        {
            return files.Select(filePath =>
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileName = fileInfo.Name;
                    var language = _languageService.GetLanguageFromText(fileName) ?? _defaultCulture;
                    var isAudioDescription = _audioDescriptionService.IsAudioDescription(fileName);

                    return new FileInput { File = fileInfo, Language = language, IsAudioDescription = isAudioDescription };
                })
                .GroupBy(file =>
                {
                    var season = _fileInfoService.ExtractSeasonNumber(file.File.Name);
                    var episode = _fileInfoService.ExtractEpisodeNumber(file.File.Name);
                    return (Season: season, Episode: episode);
                });
        }

        private async Task ProcessEpisodeGroupAsync(
            (int Season, int Episode) episodeInfo,
            System.Collections.Generic.List<FileInput> files,
            string targetFolder,
            int minCount,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Verarbeite Episode S{Season:D2}E{Episode:D2} - {EpisodeTitle} mit {FileCount} Dateien", episodeInfo.Season, episodeInfo.Episode, files.First().File.Name, files.Count);
                var ageThreshold = TimeSpan.FromMinutes(60);

                if (HasDirtyFiles(files.Select(f => f.File.FullName), ageThreshold))
                {
                    _logger.LogWarning("Dateien in der Gruppe S{Season:D2}E{Episode:D2} wurden geändert oder sind in Verwendung", episodeInfo.Season, episodeInfo.Episode);
                    return;
                }

                var skipAD = Plugin.Instance!.Configuration.SkipAudioDescription;
                // Gruppiere Dateien nach Typ
                var videoFiles = files.Where(f => _mediaConverter.IsVideoFile(f) && (!skipAD || f.IsAudioDescription)).OrderBy(d => d.IsAudioDescription).ThenBy(f => f.File.Name.Length).ToList();
                var subtitleFiles = files.Where(f => _mediaConverter.IsSubtitleFile(f)).OrderBy(d => d.IsAudioDescription).ThenBy(f => f.File.Name.Length).ToList();
                var unsupportedFiles = files.Where(f => _mediaConverter.IsUnsupportedFile(f)).OrderBy(d => d.IsAudioDescription).ThenBy(f => f.File.Name.Length).ToList();

                if (videoFiles.Count < minCount)
                {
                    _logger.LogInformation("Zu wenige Videodateien in der Gruppe S{Season:D2}E{Episode:D2} (gefunden: {Found}, benötigt: {Min})", episodeInfo.Season, episodeInfo.Episode, videoFiles.Count, minCount);
                    return;
                }

                // Wähle das Hauptvideo (erstes Video als Standard)
                var mainVideo = videoFiles[0];
                var additionalVideos = videoFiles.Skip(1);

                // Erstelle den Zieldateipfad
                var tempPath = new FileInfo(Path.Combine(_tempDirectory, $"{Guid.NewGuid()}.mkv"));
                var targetFileName = $"S{episodeInfo.Season:D2}E{episodeInfo.Episode:D2} - {_fileInfoService.NormalizeFileName(mainVideo.File.Name)}.mkv";
                var targetPath = Path.Combine(targetFolder, targetFileName);

                if (!Directory.Exists(_tempDirectory))
                {
                    Directory.CreateDirectory(_tempDirectory);
                }

                if (File.Exists(targetPath))
                {
                    _logger.LogInformation("Zieldatei {Target} existiert bereits. Überspringe Verarbeitung.", targetPath);
                    return;
                }

                try
                {
                    await _mediaConverter.MergeMediaAsync(
                        mainVideo,
                        additionalVideos,
                        subtitleFiles,
                        tempPath.FullName,
                        cancellationToken).ConfigureAwait(false);

                    tempPath.Refresh();
                    if (tempPath is { Exists: true, Length: > 1000 * 1000 * 100 }) // Mindestens 100 MB
                    {
                        if (!Directory.Exists(targetFolder))
                        {
                            Directory.CreateDirectory(targetFolder);
                        }

                        File.Move(tempPath.FullName, targetPath);
                        if (Plugin.Instance!.Configuration.DeleteSource)
                        {
                            foreach (var file in videoFiles.Concat(subtitleFiles).Concat(unsupportedFiles))
                            {
                                try
                                {
                                    file.File.Delete();
                                    _logger.LogTrace("Datei {File} erfolgreich gelöscht", file.File.FullName);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Fehler beim Löschen der Datei {File}", file.File.FullName);
                                }
                            }
                        }

                        _logger.LogTrace("Episode S{Season:D2}E{Episode:D2} erfolgreich verschoben nach {Target}", episodeInfo.Season, episodeInfo.Episode, targetPath);
                    }
                    else
                    {
                        _logger.LogWarning("Erstellte Datei S{Season:D2}E{Episode:D2} ist zu klein oder leer: {Path}", episodeInfo.Season, episodeInfo.Episode, tempPath.FullName);
                        return;
                    }
                }
                finally
                {
                    tempPath.Refresh();
                    // Temp-Datei löschen, auch bei Fehlern
                    if (tempPath.Exists)
                    {
                        tempPath.Delete();
                    }
                }

                _logger.LogTrace("Episode S{Season:D2}E{Episode:D2} erfolgreich verarbeitet", episodeInfo.Season, episodeInfo.Episode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Verarbeiten der Episode S{Season:D2}E{Episode:D2}", episodeInfo.Season, episodeInfo.Episode);
                throw;
            }
            finally
            {
                _logger.LogInformation("Verarbeite von Episode S{Season:D2}E{Episode:D2} - {EpisodeTitle} mit {FileCount} Dateien abgeschlossen", episodeInfo.Season, episodeInfo.Episode, files.First().File.Name, files.Count);
            }
        }

        private bool HasDirtyFiles(IEnumerable<string> files, TimeSpan? ageThreshold)
        {
            return files.Any(f => _fileInfoService.HasFileChanged(f, ageThreshold) || _fileInfoService.IsFileInUse(f));
        }
    }
}
