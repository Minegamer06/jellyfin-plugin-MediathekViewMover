using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.MediathekViewMover.LibaryExamples;
using Jellyfin.Plugin.MediathekViewMover.UserExamples;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MediathekViewMover
{
    /// <summary>
    /// Register Plugin Services.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc/>
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddSingleton<LibaryInfo>();
            serviceCollection.AddSingleton<UserInfo>();
        }
    }
}
