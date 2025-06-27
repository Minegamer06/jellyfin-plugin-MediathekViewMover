using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewMover.Models;
using Jellyfin.Plugin.MediathekViewMover.Services.Interfaces;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskProcessorService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        /// <param name="mediaConverter">Service für Medienkonvertierung.</param>
        /// <param name="fileInfoService">Service für Dateioperationen.</param>
        public TaskProcessorService(
            ILogger<TaskProcessorService> logger,
            MediaConversionService mediaConverter,
            IFileInfoService fileInfoService)
        {
            _logger = logger;
            _mediaConverter = mediaConverter;
            _fileInfoService = fileInfoService;
        }

        /// <summary>
        /// Verarbeitet einen einzelnen MoverTask.
        /// </summary>
        /// <param name="task">Der zu verarbeitende Task.</param>
        /// <param name="cancellationToken">Token für Abbruch der Operation.</param>
        /// <returns>Task für die asynchrone Operation.</returns>
        public async Task ProcessTaskAsync(MoverTask task, CancellationToken cancellationToken)
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

                var episodeGroups = GroupFilesByEpisode(files);
                foreach (var group in episodeGroups)
                {
                    var items = group.ToList();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (group.Key.Season == null || group.Key.Episode == null)
                    {
                        _logger.LogWarning("Überspringe Dateien ohne erkennbare Staffel/Episode");
                        continue;
                    }

                    if (items.Count < task.MinCount)
                    {
                        _logger.LogWarning("Folge hat zu wenige Versionen: S{Season:D2}E{Episode:D2} ({Count})", group.Key.Season, group.Key.Episode, items.Count);
                    }

                    var episodeSeason = (group.Key.Season.Value, group.Key.Episode.Value);
                    var seasonFolder = Path.Combine(task.TargetShowFolder, $"Staffel {group.Key.Season:D2}");
                    await ProcessEpisodeGroupAsync(episodeSeason, items, seasonFolder, cancellationToken)
                        .ConfigureAwait(false);
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

        private IEnumerable<IGrouping<(int? Season, int? Episode), string>> GroupFilesByEpisode(IEnumerable<string> files)
        {
            return files.GroupBy(file =>
            {
                string fileName = Path.GetFileName(file);

                var season = _fileInfoService.ExtractSeasonNumber(fileName);
                var episode = _fileInfoService.ExtractEpisodeNumber(fileName);
                return (Season: season, Episode: episode);
            });
        }

        private async Task ProcessEpisodeGroupAsync(
            (int Season, int Episode) episodeInfo,
            List<string> files,
            string targetFolder,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                var ageThreshold = TimeSpan.FromMinutes(60);
                _fileInfoService.InitializeFileWatchers(files);

                if (HasDirtyFiles(files, ageThreshold))
                {
                    _logger.LogWarning("Dateien in der Gruppe S{Season:D2}E{Episode:D2} wurden geändert oder sind in Verwendung", episodeInfo.Season, episodeInfo.Episode);
                    return;
                }

                // Warte 10 Sekunden, um festzustellen ob Dateien gerade in verwendung sind
                await Task.Delay(10000, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Verarbeitung abgebrochen für S{Season:D2}E{Episode:D2}", episodeInfo.Season, episodeInfo.Episode);
                    return;
                }

                // Prüfe erneut, ob Dateien in der Gruppe geändert wurden oder in Verwendung sind
                if (HasDirtyFiles(files, ageThreshold))
                {
                    _logger.LogWarning("Dateien in der Gruppe S{Season:D2}E{Episode:D2} wurden geändert oder sind in Verwendung", episodeInfo.Season, episodeInfo.Episode);
                    return;
                }

                // Gruppiere Dateien nach Typ
                var videoFiles = files.Where(f => _mediaConverter.IsVideoFile(f)).ToList();

                var subtitleFiles = files.Where(f => _mediaConverter.IsSubtitleFile(f)).ToList();

                var unsupportedFiles = files
                    .Where(f => _mediaConverter.IsUnsupportedFile(f))
                    .ToList();

                if (videoFiles.Count == 0)
                {
                    _logger.LogWarning("Keine Videodateien in der Gruppe S{Season:D2}E{Episode:D2} gefunden", episodeInfo.Season, episodeInfo.Episode);
                    return;
                }

                // Wähle das Hauptvideo (erstes Video als Standard)
                var mainVideo = videoFiles[0];
                var additionalVideos = videoFiles.Skip(1);

                // Erstelle den Zieldateipfad
                var targetFileName = $"S{episodeInfo.Season:D2}E{episodeInfo.Episode:D2} - {_fileInfoService.NormalizeFileName(mainVideo)}.mkv";
                var targetPath = Path.Combine(targetFolder, targetFileName);

                // Führe die Videos zusammen
                await _mediaConverter.MergeMediaAsync(
                    mainVideo,
                    additionalVideos,
                    subtitleFiles,
                    targetPath,
                    cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Episode S{Season:D2}E{Episode:D2} erfolgreich verarbeitet", episodeInfo.Season, episodeInfo.Episode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Verarbeiten der Episode S{Season:D2}E{Episode:D2}", episodeInfo.Season, episodeInfo.Episode);
                throw;
            }
        }

        private bool HasDirtyFiles(IEnumerable<string> files, TimeSpan? ageThreshold)
        {
            return files.Any(f => _fileInfoService.HasFileChanged(f, ageThreshold) || _fileInfoService.IsFileInUse(f));
        }
    }
}
