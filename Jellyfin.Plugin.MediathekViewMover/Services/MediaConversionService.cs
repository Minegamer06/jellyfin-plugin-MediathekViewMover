using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.Services
{
    /// <summary>
    /// Service zur Verarbeitung von Mediendateien mit FFmpeg.
    /// </summary>
    public class MediaConversionService
    {
        private readonly ILogger<MediaConversionService> _logger;
        private readonly LanguageService _languageService;
        private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov"];
        private static readonly string[] SubtitleExtensions = [".srt", ".ass", ".ssa"];
        private static readonly string[] UnsupportedExtensions = [".ttml"];

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaConversionService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        /// <param name="languageService">Der Service für Spracherkennung.</param>
        public MediaConversionService(ILogger<MediaConversionService> logger, LanguageService languageService)
        {
            _logger = logger;
            _languageService = languageService;
        }

        /// <summary>
        /// Prüft, ob eine Datei ein Video ist.
        /// </summary>
        /// <param name="filePath">Der Dateipfad.</param>
        /// <returns>True wenn die Datei ein Video ist, sonst false.</returns>
        public bool IsVideoFile(string filePath)
        {
            return VideoExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
        }

        /// <summary>
        /// Prüft, ob eine Datei ein Untertitel ist.
        /// </summary>
        /// <param name="filePath">Der Dateipfad.</param>
        /// <returns>True wenn die Datei ein Untertitel ist, sonst false.</returns>
        public bool IsSubtitleFile(string filePath)
        {
            return SubtitleExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
        }

        /// <summary>
        /// Prüft, ob eine Datei ein nicht unterstütztes Format ist. Sollten nur Dateien die Heruntergeladen werden aber nicht verarbeitet werden können sein.
        /// </summary>
        /// <param name="filePath">Der Dateipfad.</param>
        /// <returns>True wenn die Datei nicht unterstützt wird, sonst false.</returns>
        public bool IsUnsupportedFile(string filePath)
        {
            return UnsupportedExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
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
            string mainVideo,
            IEnumerable<string> additionalVideos,
            IEnumerable<string> subtitles,
            string targetPath,
            CancellationToken cancellationToken)
        {
            try
            {
                ValidateInputFile(mainVideo);
                var additionalFiles = additionalVideos.ToList();
                var subtitleFiles = subtitles.ToList();

                foreach (var file in additionalFiles.Concat(subtitleFiles))
                {
                    ValidateInputFile(file);
                }

                // Erkenne Sprache des Hauptvideos
                var mainLang = _languageService.GetLanguageFromFileName(mainVideo);
                var args = FFMpegArguments.FromFileInput(mainVideo);

                var selectedStream = new List<(string FilePath, Channel Channel)>();
                // Füge zusätzliche Videos als Input hinzu
                foreach (var file in additionalFiles)
                {
                    args.AddFileInput(file);
                    selectedStream.Add((file, Channel.Audio));
                }

                // Füge Untertitel hinzu
                foreach (var file in subtitleFiles)
                {
                    args.AddFileInput(file);
                    selectedStream.Add((file, Channel.Subtitle));
                }

                await args.OutputToFile(targetPath, true, options =>
                {
                    // Kopiere Video vom Hauptvideo
                    // Setze die Hauptaudiospur
                    options
                        .WithAudioCodec(0, AudioCodec.Copy)
                        .WithCustomArgument($"-metadata:s:a:0 language={mainLang}")
                        .WithCustomArgument("-disposition:a:0 default");

                    // Füge zusätzliche Audiospuren hinzu
                    for (int i = 0; i < additionalFiles.Count; i++)
                    {
                        var lang = _languageService.GetLanguageFromFileName(additionalFiles[i]);
                        options
                            .WithAudioCodec(i + 1, AudioCodec.Copy)
                            .WithCustomArgument($"-metadata:s:a:{i + 1} language={lang}")
                            .WithCustomArgument($"-disposition:a:{i + 1} 0")
                            .WithCustomArgument($"-map {i + 1}:a:0");
                    }

                    // Füge Untertitel hinzu
                    for (int i = 0; i < subtitleFiles.Count; i++)
                    {
                        var lang = _languageService.GetLanguageFromFileName(subtitleFiles[i]);
                        var inputIndex = additionalFiles.Count + i + 1;
                        options
                            .WithCustomArgument($"-map {inputIndex}:0")
                            .WithCustomArgument($"-metadata:s:s:{i} language={lang}")
                            .WithCustomArgument($"-disposition:s:{i} 0");
                    }
                })
                .ProcessAsynchronously(true, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Medien erfolgreich zusammengeführt: {Main} + {Additional} Audiospuren + {Subs} Untertitel -> {Target}",
                    mainVideo,
                    additionalFiles.Count,
                    subtitleFiles.Count,
                    targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler beim Zusammenführen der Medien: {Source}", mainVideo);
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
