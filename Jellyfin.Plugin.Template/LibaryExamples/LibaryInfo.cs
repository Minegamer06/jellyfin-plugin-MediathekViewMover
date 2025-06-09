using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Template.LibaryExamples
{
    /// <summary>
    /// Info Class.
    /// </summary>
    public class LibaryInfo
    {
        private readonly ILogger<LibaryInfo> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly ICollectionManager _collectionManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="LibaryInfo"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{LibaryInfo}"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="collectionManager">Instance of the <see cref="ICollectionManager"/> interface.</param>
        public LibaryInfo(ILogger<LibaryInfo> logger, ILibraryManager libraryManager, ICollectionManager collectionManager)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _collectionManager = collectionManager;
        }

        /// <summary>
        /// Run Example.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        public async Task Run()
        {
            _logger.LogInformation($"Start - {nameof(LibaryInfo)}");

            // Count Videos
            var count = _libraryManager.GetCount(new InternalItemsQuery() { MediaTypes = [MediaType.Video] });
            _logger.LogInformation("MSG - {LibaryInfo} - Video Count: {LibCount}", nameof(LibaryInfo), count);

            // Count HD Movies
            var movies = _libraryManager.GetItemList(new InternalItemsQuery() { IncludeItemTypes = [BaseItemKind.Movie] }).Select(m => m as Movie).Where(m => m?.IsHD ?? false).ToList();
            _logger.LogInformation("MSG - {LibaryInfo} - Movie HD Count: {LibCount}", nameof(LibaryInfo), movies.Count);

            // Add ore Remove Tag To/From Media
            if (movies?.Any(m => m?.IsHD ?? false) ?? false)
            {
                Movie movie = movies.First(m => m?.IsHD ?? false)!;
                if (!movie.Tags.Any(t => t == "Mini Movie"))
                {
                    movie.AddTag("Mini Movie");
                }
                else
                {
                    movie.Tags = movie.Tags.Where(t => t != "Mini Movie").ToArray();
                }

                CancellationToken token = CancellationToken.None;
                var parent = movie.GetParent();
                await _libraryManager.UpdateItemAsync(movie, parent, ItemUpdateType.MetadataEdit, token).ConfigureAwait(false);
                _logger.LogInformation("MSG - {LibaryInfo} - Tag Update: {MovieName}", nameof(LibaryInfo), movie.Name);
            }

            // Collection Manager
            _logger.LogInformation("MSG - {LibaryInfo} - Collection Manager:", nameof(LibaryInfo));
            var collections = _libraryManager.GetItemList(new InternalItemsQuery() { IncludeItemTypes = [BaseItemKind.BoxSet] }).Select(s => s as BoxSet).ToList();
            var collection = collections.FirstOrDefault(c => c is not null);
            var collectionContent = collection!.GetRecursiveChildren();

            if (collection is not null)
            {
               await _collectionManager.AddToCollectionAsync(collection.Id, movies!.Select(x => x!.Id)).ConfigureAwait(false);
               _logger.LogInformation("MSG - {LibaryInfo} - Collection Info: {Collection}", nameof(LibaryInfo), collection.Name);
            }

            _logger.LogInformation($"Stop - {nameof(LibaryInfo)}");
        }
    }
}
