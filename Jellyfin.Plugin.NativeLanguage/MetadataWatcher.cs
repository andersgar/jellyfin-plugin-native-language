using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NativeLanguage;

/// <summary>
/// Handles new movies and series as soon as Jellyfin has downloaded their metadata.
/// </summary>
public class MetadataWatcher : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly NativeTitleService _service;
    private readonly ILogger<MetadataWatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataWatcher"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="service">Title service.</param>
    /// <param name="logger">Logger.</param>
    public MetadataWatcher(ILibraryManager libraryManager, NativeTitleService service, ILogger<MetadataWatcher> logger)
    {
        _libraryManager = libraryManager;
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated += OnItemUpdated;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemUpdated -= OnItemUpdated;
        return Task.CompletedTask;
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        // Only react to fresh metadata; our own edits are saved as MetadataEdit and must not loop.
        if (e.Item is not (Movie or Series)
            || (e.UpdateReason & (ItemUpdateType.MetadataDownload | ItemUpdateType.MetadataImport)) == 0)
        {
            return;
        }

        var item = e.Item;
        _ = Task.Run(async () =>
        {
            try
            {
                await _service.ProcessItemAsync(item, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Native Language could not handle {Name}", item.Name);
            }
        });
    }
}
