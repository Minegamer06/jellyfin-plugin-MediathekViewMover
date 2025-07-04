using System.Collections.Generic;
using Jellyfin.Plugin.MediathekViewMover.Models;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediathekViewMover.Configuration;

/// <summary>
/// The configuration options.
/// </summary>
public enum SomeOptions
{
    /// <summary>
    /// Option one.
    /// </summary>
    OneOption,

    /// <summary>
    /// Second option.
    /// </summary>
    AnotherOption
}

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
        // set default options here
        Options = SomeOptions.AnotherOption;
        TrueFalseSetting = true;
        AnInteger = 2;
        AString = "string";
        MoverTasks = [];
    }

    /// <summary>
    /// Gets or sets a value indicating whether some true or false setting is enabled.
    /// </summary>
    public bool TrueFalseSetting { get; set; }

    /// <summary>
    /// Gets or sets an integer setting.
    /// </summary>
    public int AnInteger { get; set; }

    /// <summary>
    /// Gets or sets a string setting.
    /// </summary>
    public string AString { get; set; }

    /// <summary>
    /// Gets or sets an enum option.
    /// </summary>
    public SomeOptions Options { get; set; }

    /// <summary>
    /// Gets a list of MoverTasks.
    /// </summary>
    #pragma warning disable CA1002
    public List<MoverTask> MoverTasks { get; }
    #pragma warning restore CA1002
}
