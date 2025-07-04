using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.MediathekViewMover.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.Services
{
    /// <summary>
    /// Service zur Verwaltung von Dateiinformationen und Überwachung von Dateiänderungen.
    /// </summary>
    public class FileInfoService : IFileInfoService, IDisposable
    {
        private readonly ILogger<FileInfoService> _logger;
        private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers;
        private readonly ConcurrentDictionary<string, DateTime> _fileTimestamps;
        private readonly ConcurrentDictionary<string, long> _fileSizes;
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _fileMetadata;
        private bool _disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileInfoService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        public FileInfoService(ILogger<FileInfoService> logger)
        {
            _logger = logger;
            _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
            _fileTimestamps = new ConcurrentDictionary<string, DateTime>();
            _fileSizes = new ConcurrentDictionary<string, long>();
            _fileMetadata = new ConcurrentDictionary<string, Dictionary<string, object>>();
        }

        /// <summary>
        /// Initialisiert die Überwachung für eine Liste von Dateien.
        /// </summary>
        /// <param name="filePaths">Die Pfade der zu überwachenden Dateien.</param>
        public void InitializeFileWatchers(IEnumerable<string> filePaths)
        {
            var paths = filePaths as string[] ?? filePaths.ToArray();
            foreach (var dir in paths.Select(d => new FileInfo(d)?.Directory?.FullName).Distinct())
            {
                if (string.IsNullOrEmpty(dir))
                {
                    continue;
                }

                InitializeWatcher(dir);
            }

            foreach (var file in paths)
            {
                ResetChangedState(file);
            }
        }

        /// <summary>
        /// Initialisiert die Überwachung für einen bestimmten Pfad.
        /// </summary>
        /// <param name="path">Der zu überwachende Pfad.</param>
        public void InitializeWatcher(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                _logger.LogError("Ungültiger Pfad für FileSystemWatcher: {Path}", path);
                return;
            }

            if (_watchers.ContainsKey(path))
            {
                _logger.LogInformation("Watcher existiert bereits für Pfad: {Path}", path);
                return;
            }

            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnFileChanged;
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileDeleted;
            watcher.Renamed += OnFileRenamed;

            _watchers.TryAdd(path, watcher);
            _logger.LogInformation("FileSystemWatcher initialisiert für: {Path}", path);
        }

        /// <summary>
        /// Prüft, ob sich eine Datei seit dem letzten Check geändert hat.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        /// <param name="ageThreshold">Mindestalter welches die Datei haben muss.</param>
        /// <returns>True wenn sich die Datei geändert hat, sonst false.</returns>
        public bool HasFileChanged(string filePath, TimeSpan? ageThreshold = null)
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                return false;
            }

            ageThreshold ??= TimeSpan.Zero;
            var currentTime = fileInfo.LastWriteTime;
            if (_fileTimestamps.TryGetValue(filePath, out DateTime lastCheck) && _fileSizes.TryGetValue(filePath, out long fileSize))
            {
                return currentTime > (DateTime.Now - ageThreshold) || currentTime > lastCheck || fileInfo.Length != fileSize;
            }

            _fileTimestamps.TryAdd(filePath, currentTime);
            _fileSizes.TryAdd(filePath, fileInfo.Length);
            return true;
        }

        /// <summary>
        /// Prüft, ob eine Datei gerade verwendet wird.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        /// <returns>True wenn die Datei in Verwendung ist, sonst false.</returns>
        public bool IsFileInUse(string filePath)
        {
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        /// <summary>
        /// Aktualisiert den Zeitstempel der letzten Überprüfung für eine Datei.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        public void ResetChangedState(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                _fileTimestamps.AddOrUpdate(filePath, fileInfo.LastWriteTime, (key, oldValue) => fileInfo.LastWriteTime);
                _fileSizes.AddOrUpdate(filePath, fileInfo.Length, (key, oldValue) => fileInfo.Length);
            }
        }

        /// <summary>
        /// Normalisiert einen Dateinamen nach dem Schema des PowerShell-Skripts.
        /// </summary>
        /// <param name="fileName">Der zu normalisierende Dateiname.</param>
        /// <returns>Der normalisierte Dateiname.</returns>
        public string NormalizeFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            // Regex-Pattern aus dem PowerShell-Skript übernommen
            var pattern = @"(?:Folge_\d+)?[_\-\s]{0,2}(?<title>.+?)\((?<scode>S\d+[_\-\s\/]{0,3}E\d+)\)";
            var match = Regex.Match(baseName, pattern);

            if (match.Success)
            {
                var title = match.Groups["title"].Value.Trim(" -_,".ToCharArray());
                var seasonCode = match.Groups["scode"].Value;
                // Mehrfache Bindestriche, Unterstriche und Leerzeichen durch ein einzelnes Leerzeichen ersetzen
                title = Regex.Replace(title, @"[-_,\s]+", " ");
                return TrimUnwantedCharacters(title); // $"{seasonCode} - {title}{extension}";
            }

            // Fallback für alternative Formate
            var fallbackPattern = @"(?:\()?(?<scode>S\d+[_\-\s\/]{0,3}E\d+)(?:\))?";
            match = Regex.Match(baseName, fallbackPattern);
            if (match.Success)
            {
                var seasonCode = match.Groups["scode"].Value;
                var remainingTitle = baseName.Replace(match.Value, string.Empty, StringComparison.InvariantCultureIgnoreCase)
                    .Trim(" -_,".ToCharArray());
                remainingTitle = Regex.Replace(remainingTitle, @"[-_,\s]+", " ");
                return TrimUnwantedCharacters(remainingTitle); // $"{seasonCode} - {remainingTitle}{extension}";
            }

            return TrimUnwantedCharacters(fileName);
        }

        private string TrimUnwantedCharacters(string input)
        {
            // Entfernt unerwünschte Zeichen am Anfang und Ende des Strings
            return input.Trim(" -_,:;()[]{}&".ToCharArray());
        }

        /// <summary>
        /// Extrahiert die Staffelnummer aus einem Dateinamen.
        /// </summary>
        /// <param name="fileName">Der Dateiname.</param>
        /// <returns>Die Staffelnummer oder null wenn keine gefunden wurde.</returns>
        public int? ExtractSeasonNumber(string fileName)
        {
            var match = Regex.Match(fileName, @"S(?<Number>\d{1,2})");
            if (match.Success && int.TryParse(match.Groups["Number"].Value, out int seasonNumber))
            {
                return seasonNumber;
            }

            _logger.LogWarning("Keine Staffelnummer gefunden in: {FileName}", fileName);
            return null;
        }

        /// <summary>
        /// Extrahiert die Episodennummer aus einem Dateinamen.
        /// </summary>
        /// <param name="fileName">Der Dateiname.</param>
        /// <returns>Die Episodennummer oder 0 wenn keine gefunden wurde.</returns>
        public int? ExtractEpisodeNumber(string fileName)
        {
            var match = Regex.Match(fileName, @"[Ee](?<episode>\d{1,2})");
            if (match.Success && int.TryParse(match.Groups["episode"].Value, out int episode))
            {
                return episode;
            }

            _logger.LogWarning("Keine Episodennummer gefunden in: {File}", fileName);
            return null;
        }

        /// <summary>
        /// Gibt die Metadaten einer Datei zurück.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        /// <returns>Dictionary mit Metadaten.</returns>
        public Dictionary<string, object> GetFileMetadata(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, object>();
            }

            if (_fileMetadata.TryGetValue(filePath, out var metadata))
            {
                return metadata;
            }

            var fileInfo = new FileInfo(filePath);
            var newMetadata = new Dictionary<string, object>
            {
                { "FileName", fileInfo.Name },
                { "NormalizedName", NormalizeFileName(fileInfo.Name) },
                { "SeasonNumber", ExtractSeasonNumber(fileInfo.Name)! },
                { "EpisodeNumber", ExtractEpisodeNumber(fileInfo.Name)! },
                { "Size", fileInfo.Length },
                { "CreationTime", fileInfo.CreationTime },
                { "LastWriteTime", fileInfo.LastWriteTime },
                { "LastAccessTime", fileInfo.LastAccessTime }
            };

            _fileMetadata.TryAdd(filePath, newMetadata);
            return newMetadata;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("Datei geändert: {Path}", e.FullPath);
            _fileTimestamps.TryRemove(e.FullPath, out _);
            _fileMetadata.TryRemove(e.FullPath, out _);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            _logger.LogInformation("Datei gelöscht: {Path}", e.FullPath);
            _fileTimestamps.TryRemove(e.FullPath, out _);
            _fileMetadata.TryRemove(e.FullPath, out _);
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation("Datei umbenannt von {OldPath} nach {NewPath}", e.OldFullPath, e.FullPath);
            _fileTimestamps.TryRemove(e.OldFullPath, out _);
            _fileMetadata.TryRemove(e.OldFullPath, out _);
        }

        /// <summary>
        /// Disposes the resources used by the <see cref="FileInfoService"/>.
        /// </summary>
        /// <param name="disposing">True, um verwaltete und nicht verwaltete Ressourcen freizugeben; false, um nur nicht verwaltete Ressourcen freizugeben.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    foreach (var watcher in _watchers.Values)
                    {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    }

                    _watchers.Clear();
                    _fileTimestamps.Clear();
                    _fileMetadata.Clear();
                }

                _disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
