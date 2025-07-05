using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using Jellyfin.Plugin.MediathekViewMover.Models;
using Jellyfin.Plugin.MediathekViewMover.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.Services
{
    /// <summary>
    /// Service zur Verarbeitung von Mediendateien mit FFmpeg.
    /// </summary>
    public class MediaConversionService
    {
        private const long SmallFileThreshold = 1024 * 1024; // 1 MB
        private readonly ILogger<MediaConversionService> _logger;
        private readonly LanguageService _languageService;
        private readonly IAudioDescriptionService _audioDescriptionService;
        private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov"];
        private static readonly string[] SubtitleExtensions = [".srt", ".ass", ".ssa"];
        private static readonly string[] UnsupportedExtensions = [".ttml", ".jpg"];

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaConversionService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        /// <param name="languageService">Der Service für Spracherkennung.</param>
        /// <param name="audioDescriptionService">Der Service für Audiodeskription.</param>
        public MediaConversionService(
            ILogger<MediaConversionService> logger,
            LanguageService languageService,
            IAudioDescriptionService audioDescriptionService)
        {
            _logger = logger;
            _languageService = languageService;
            _audioDescriptionService = audioDescriptionService;
        }

        /// <summary>
        /// Prüft, ob eine Datei ein Video ist.
        /// </summary>
        /// <param name="file">Die Datei.</param>
        /// <returns>True wenn die Datei ein Video ist, sonst false.</returns>
        public bool IsVideoFile(FileInput file)
        {
            return VideoExtensions.Contains(file.File.Extension.ToLowerInvariant());
        }

        /// <summary>
        /// Prüft, ob eine Datei ein Untertitel ist.
        /// </summary>
        /// <param name="file">Die Datei.</param>
        /// <returns>True wenn die Datei ein Untertitel ist, sonst false.</returns>
        public bool IsSubtitleFile(FileInput file)
        {
            return SubtitleExtensions.Contains(file.File.Extension.ToLowerInvariant());
        }

        /// <summary>
        /// Prüft, ob eine Datei ein nicht unterstütztes Format ist.
        /// </summary>
        /// <param name="file">Die Datei.</param>
        /// <returns>True wenn die Datei nicht unterstützt wird, sonst false.</returns>
        public bool IsUnsupportedFile(FileInput file)
        {
            return UnsupportedExtensions.Contains(file.File.Extension.ToLowerInvariant());
        }

        /// <summary>
        /// Führt mehrere Videos mit ihren Audiospuren zusammen.
        /// </summary>
        /// <param name="mainVideo">Das Hauptvideo.</param>
        /// <param name="additionalVideos">Zusätzliche Videos mit Audiospuren.</param>
        /// <param name="subtitles">Untertiteldateien.</param>
        /// <param name="targetPath">Zielpfad.</param>
        /// <param name="cancellationToken">Token für Abbruch der Operation.</param>
        /// <returns><see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task MergeMediaAsync(
            FileInput mainVideo,
            IEnumerable<FileInput> additionalVideos,
            IEnumerable<FileInput> subtitles,
            string targetPath,
            CancellationToken cancellationToken)
        {
            try
            {
                ValidateInputFile(mainVideo.File.FullName);
                var skipAD = Plugin.Instance!.Configuration.SkipAudioDescription;
                var additionalFiles = additionalVideos
                    .Where(f => !skipAD || !f.IsAudioDescription)
                    .ToList();

                // Dedupliziere kleine Untertitel-Dateien basierend auf Hash
                var subtitleFiles = subtitles
                    .Where(f => !skipAD || !f.IsAudioDescription)
                    .GroupBy(f => f.File.Length < SmallFileThreshold ? f.Hash : Guid.NewGuid().ToString())
                    .Select(g => g.First())
                    .ToList();

                foreach (var file in additionalFiles.Concat(subtitleFiles))
                {
                    ValidateInputFile(file.File.FullName);
                }

                var args = FFMpegArguments.FromFileInput(mainVideo.File.FullName);

                foreach (var file in additionalFiles)
                {
                    args.AddFileInput(file.File.FullName);
                }

                // Füge Untertitel hinzu
                foreach (var file in subtitleFiles)
                {
                    args.AddFileInput(file.File.FullName);
                }

                await args.OutputToFile(targetPath, true, options =>
                {
                    // Setze allgemeine Codec-Argumente für Audio und Untertitel
                    options.WithCustomArgument("-c:a copy")
                        .WithCustomArgument("-c:s copy")
                        .WithCustomArgument("-c:v copy");

                    // Kopiere Video vom Hauptvideo
                    options.SelectStream(0, 0, Channel.Video)
                        .SelectStream(0, 0, Channel.Audio);
                    // Setze die Hauptaudiospur
                    options
                        .WithCustomArgument($"-metadata:s:a:0 language={mainVideo.Language.ThreeLetterISOLanguageName}")
                        .WithCustomArgument("-disposition:a:0 default");

                    // Füge zusätzliche Audiospuren hinzu
                    for (int i = 0; i < additionalFiles.Count; i++)
                    {
                        var file = additionalFiles[i];

                        options
                            .SelectStream(0, i + 1, Channel.Audio)
                            .WithCustomArgument($"-metadata:s:a:{i + 1} language={file.Language.ThreeLetterISOLanguageName}");
                        if (file.IsAudioDescription)
                        {
                            options.WithCustomArgument($"-metadata:s:a:{i + 1} title=\"Audio Description\"")
                                .WithCustomArgument($"-metadata:s:a:{i + 1} handler_name=\"Audio Description\"")
                                .WithCustomArgument($"-disposition:a:{i + 1} +visual_impaired");
                        }
                        else
                        {
                            options.WithCustomArgument($"-disposition:a:{i + 1} 0");
                        }
                    }

                    // Füge Untertitel hinzu
                    for (int i = 0; i < subtitleFiles.Count; i++)
                    {
                        var inputIndex = additionalFiles.Count + i + 1;
                        options
                            .SelectStream(0, inputIndex, Channel.Subtitle)
                            .WithCustomArgument($"-metadata:s:s:{i} language={subtitleFiles[i].Language.ThreeLetterISOLanguageName}")
                            .WithCustomArgument($"-disposition:s:{i} 0");
                        if (subtitleFiles[i].IsAudioDescription)
                        {
                            options.WithCustomArgument($"-metadata:s:s:{i} title=\"Audio Description\"");
                        }
                    }
                })
                .ProcessAsynchronously().ConfigureAwait(false);

                _logger.LogInformation(
                    "Medien erfolgreich zusammengeführt: {Main} + {Additional} Audiospuren + {Subs} Untertitel -> {Target}",
                    mainVideo.File.Name,
                    additionalFiles.Count,
                    subtitleFiles.Count,
                    targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Zusammenführen der Medien: {Source}", mainVideo.File.Name);
                throw;
            }
        }

        private void ValidateInputFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Dateipfad darf nicht leer sein.", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Mediendatei nicht gefunden.", filePath);
            }
        }
    }
}
