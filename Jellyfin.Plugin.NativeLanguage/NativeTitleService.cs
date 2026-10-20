using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NativeLanguage;

/// <summary>
/// Gives movies and series in the configured original languages their original title, and locks it.
/// </summary>
public class NativeTitleService
{
    private readonly ILibraryManager _libraryManager;
    private readonly TmdbLanguageClient _tmdb;
    private readonly ILogger<NativeTitleService> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="NativeTitleService"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="tmdb">TMDb client.</param>
    /// <param name="logger">Logger.</param>
    public NativeTitleService(ILibraryManager libraryManager, TmdbLanguageClient tmdb, ILogger<NativeTitleService> logger)
    {
        _libraryManager = libraryManager;
        _tmdb = tmdb;
        _logger = logger;
    }

    /// <summary>
    /// Checks every movie and series in the library.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ProcessAllAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        if (!IsUsable(config))
        {
            progress.Report(100);
            return;
        }

        var kinds = new List<BaseItemKind>();
        if (config.IncludeMovies)
        {
            kinds.Add(BaseItemKind.Movie);
        }

        if (config.IncludeSeries)
        {
            kinds.Add(BaseItemKind.Series);
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = kinds.ToArray(),
            IsVirtualItem = false,
            Recursive = true
        });

        await _runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = 0;
            for (var i = 0; i < items.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await ProcessItemCoreAsync(items[i], cancellationToken).ConfigureAwait(false))
                {
                    changed++;
                }

                progress.Report(100.0 * (i + 1) / items.Count);
            }

            _logger.LogInformation(
                "Native Language checked {Count} items, {Changed} {Verb}",
                items.Count,
                changed,
                config.DryRun ? "would change (dry run)" : "changed");
        }
        finally
        {
            _runLock.Release();
        }
    }

    /// <summary>
    /// Checks a single item, e.g. right after its metadata was downloaded.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ProcessItemAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (!IsUsable(Plugin.Instance!.Configuration))
        {
            return;
        }

        await _runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ProcessItemCoreAsync(item, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task<bool> ProcessItemCoreAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var isSeries = item is Series;
        if ((isSeries && !config.IncludeSeries) || (item is Movie && !config.IncludeMovies) || (!isSeries && item is not Movie))
        {
            return false;
        }

        TmdbTitleInfo? info;
        try
        {
            info = await _tmdb.GetAsync(
                isSeries,
                item.GetProviderId(MetadataProvider.Tmdb),
                item.GetProviderId(MetadataProvider.Tvdb),
                item.GetProviderId(MetadataProvider.Imdb),
                config.TmdbApiKey.Trim(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TMDb lookup failed for {Name}", item.Name);
            return false;
        }

        if (info is null || !ParseLanguages(config.Languages).Contains(info.Language))
        {
            return false;
        }

        // Prefer Jellyfin's own original title; fall back to TMDb's.
        var target = !string.IsNullOrWhiteSpace(item.OriginalTitle) ? item.OriginalTitle.Trim() : info.OriginalTitle.Trim();
        if (string.IsNullOrEmpty(target))
        {
            return false;
        }

        var locked = item.LockedFields.Contains(MetadataField.Name);
        var needsRename = !string.Equals(item.Name, target, StringComparison.Ordinal);
        var needsLock = config.LockName && !locked;
        if (!needsRename && !needsLock)
        {
            return false;
        }

        if (config.DryRun)
        {
            _logger.LogInformation("Native Language (dry run): {Old} -> {New}", item.Name, target);
            return true;
        }

        if (needsRename)
        {
            _logger.LogInformation("Native Language: {Old} -> {New}", item.Name, target);
            item.Name = target;
            item.SortName = null!;
        }

        if (needsLock)
        {
            item.LockedFields = item.LockedFields.Append(MetadataField.Name).ToArray();
        }

        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private bool IsUsable(Configuration.PluginConfiguration config)
    {
        if (!config.Enabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("Native Language needs a TMDb API key in its settings");
            return false;
        }

        return true;
    }

    private static HashSet<string> ParseLanguages(string languages) =>
        languages
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);
}
