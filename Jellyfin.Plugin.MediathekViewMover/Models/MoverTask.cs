// <copyright file="MoverTask.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

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
    /// Gets or sets minimale Anzahl Dateien.
    /// </summary>
    public int MinCount { get; set; }

    /// <summary>
    /// Gets or sets quell-Ordner.
    /// </summary>
    public string SourceShowFolder { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets ziel-Ordner.
    /// </summary>
    public string TargetShowFolder { get; set; } = string.Empty;
}
