using System.Collections.Generic;

namespace Jellyfin.Plugin.MediathekViewMover.Services.Interfaces
{
    /// <summary>
    /// Service zur Erkennung von Audiodeskription.
    /// </summary>
    public interface IAudioDescriptionService
    {
        /// <summary>
        /// Prüft ob eine Datei eine Audiodeskription enthält.
        /// </summary>
        /// <param name="filePath">Der zu prüfende Dateipfad.</param>
        /// <returns>True wenn die Datei eine Audiodeskription ist, sonst false.</returns>
        bool IsAudioDescription(string filePath);
    }
}
