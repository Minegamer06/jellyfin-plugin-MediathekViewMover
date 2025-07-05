using System.Collections.Generic;
using System.Runtime.Serialization;
using Jellyfin.Plugin.MediathekViewMover.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediathekViewMover.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        DeleteSource = false;
        MoverTasks = [];
        AudioDescriptionPatterns = new[] { "Audiodeskription", "_AD" };
        SkipAudioDescription = false;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the source should be deleted after processing.
    /// </summary>
    public bool DeleteSource { get; set; }

    /// <summary>
    /// Gets or sets a list of MoverTasks.
    /// </summary>
    [DataMember]
    public List<MoverTask> MoverTasks { get; set; }

    /// <summary>
    /// Gets or sets the patterns to identify audio description files.
    /// </summary>
    public string[] AudioDescriptionPatterns { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether audio description tracks should be skipped.
    /// </summary>
    public bool SkipAudioDescription { get; set; }
}
