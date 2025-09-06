namespace Jellyfin.Plugin.MediathekViewMover.Models;

/// <summary>
/// The MoverTask is a Model for a task e.g. a Series that should be moved (and merged, renamed, etc.) to a different folder. From Sources like MediathekView or other sources.
/// </summary>
public class MoverTask
{
    /// <summary>
    /// Gets or sets titel für die Zuordnung.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets minimale Anzahl an Video Dateien.
    /// </summary>
    public int MinCount { get; set; }

    /// <summary>
    /// Gets or sets the source folder.
    /// </summary>
    public string SourceShowFolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target folder.
    /// </summary>
    public string TargetShowFolder { get; set; } = string.Empty;
}
