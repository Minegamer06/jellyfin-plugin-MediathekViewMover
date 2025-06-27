using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MediathekViewMover.Services.Interfaces
{
    /// <summary>
    /// Service für die Überwachung von Dateiänderungen.
    /// </summary>
    public interface IFileInfoService
    {
        /// <summary>
        /// Initialisiert die Überwachung für eine Liste von Dateien.
        /// </summary>
        /// <param name="filePaths">Die Pfade der zu überwachenden Dateien.</param>
        void InitializeFileWatchers(IEnumerable<string> filePaths);

        /// <summary>
        /// Initialisiert die Überwachung für einen bestimmten Pfad.
        /// </summary>
        /// <param name="path">Der zu überwachende Pfad.</param>
        void InitializeWatcher(string path);

        /// <summary>
        /// Prüft, ob sich eine Datei seit dem letzten Check geändert hat.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        /// <param name="ageThreshold">Mindestalter welches die Datei haben muss.</param>
        /// <returns>True wenn sich die Datei geändert hat, sonst false.</returns>
        bool HasFileChanged(string filePath, TimeSpan? ageThreshold = null);

        /// <summary>
        /// Prüft, ob eine Datei gerade verwendet wird.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        /// <returns>True wenn die Datei in Verwendung ist, sonst false.</returns>
        bool IsFileInUse(string filePath);

        /// <summary>
        /// Aktualisiert den Zeitstempel der letzten Überprüfung für eine Datei.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        void ResetChangedState(string filePath);

        /// <summary>
        /// Gibt die Metadaten einer Datei zurück.
        /// </summary>
        /// <param name="filePath">Pfad zur Datei.</param>
        /// <returns>Dictionary mit Metadaten.</returns>
        Dictionary<string, object> GetFileMetadata(string filePath);

        /// <summary>
        /// Normalisiert einen Dateinamen nach dem Schema des PowerShell-Skripts.
        /// </summary>
        /// <param name="fileName">Der zu normalisierende Dateiname.</param>
        /// <returns>Der normalisierte Dateiname.</returns>
        string NormalizeFileName(string fileName);

        /// <summary>
        /// Extrahiert die Staffelnummer aus einem Dateinamen.
        /// </summary>
        /// <param name="fileName">Der Dateiname.</param>
        /// <returns>Die Staffelnummer oder null wenn keine gefunden wurde.</returns>
        int? ExtractSeasonNumber(string fileName);

        /// <summary>
        /// Extrahiert die Episodennummer aus einem Dateinamen.
        /// </summary>
        /// <param name="fileName">Der Dateiname.</param>
        /// <returns>Die Episodennummer oder 0 wenn keine gefunden wurde.</returns>
        int? ExtractEpisodeNumber(string fileName);
    }
}
