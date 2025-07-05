using System;
using System.Linq;
using Jellyfin.Plugin.MediathekViewMover.Configuration;
using Jellyfin.Plugin.MediathekViewMover.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.Services
{
    /// <summary>
    /// Service zur Erkennung von Audiodeskription.
    /// </summary>
    public class AudioDescriptionService : IAudioDescriptionService
    {
        private readonly ILogger<AudioDescriptionService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioDescriptionService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        public AudioDescriptionService(ILogger<AudioDescriptionService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool IsAudioDescription(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var config = Plugin.Instance!.Configuration;
            if (config.AudioDescriptionPatterns.Length == 0)
            {
                // Standardmuster falls keine Konfiguration vorhanden
                return filePath.Contains("Audiodeskription", StringComparison.OrdinalIgnoreCase) ||
                       filePath.Contains("_AD", StringComparison.OrdinalIgnoreCase);
            }

            return config.AudioDescriptionPatterns.Any(pattern =>
                filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }
    }
}
