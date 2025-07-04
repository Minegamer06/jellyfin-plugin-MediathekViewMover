using Jellyfin.Plugin.MediathekViewMover.Services;
using Jellyfin.Plugin.MediathekViewMover.Services.Interfaces;
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
            serviceCollection.AddSingleton<IFileInfoService, FileInfoService>();
            serviceCollection.AddSingleton<LanguageService>();
            serviceCollection.AddSingleton<MediaConversionService>();
            serviceCollection.AddSingleton<TaskProcessorService>();
        }
    }
}
