using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NativeLanguage;

/// <summary>
/// Looks up a title's original language and original title on TMDb, with a cache on disk.
/// </summary>
public class TmdbLanguageClient
{
    private const string BaseUrl = "https://api.themoviedb.org/3/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TmdbLanguageClient> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private Dictionary<string, TmdbTitleInfo>? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TmdbLanguageClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public TmdbLanguageClient(IHttpClientFactory httpClientFactory, ILogger<TmdbLanguageClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static string CachePath =>
        Path.Combine(Plugin.Instance!.DataFolderPath, "tmdb-cache.json");

    /// <summary>
    /// Gets the original language and title for a movie or series.
    /// </summary>
    /// <param name="isSeries">True for series, false for movies.</param>
    /// <param name="tmdbId">TMDb id, if known.</param>
    /// <param name="tvdbId">TVDb id, used when the TMDb id is missing.</param>
    /// <param name="imdbId">IMDb id, used when the TMDb id is missing.</param>
    /// <param name="apiKey">TMDb v3 API key or v4 read access token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The info, or null when TMDb has no match.</returns>
    public async Task<TmdbTitleInfo?> GetAsync(
        bool isSeries,
        string? tmdbId,
        string? tvdbId,
        string? imdbId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var kind = isSeries ? "tv" : "movie";

        if (string.IsNullOrEmpty(tmdbId))
        {
            tmdbId = await FindTmdbIdAsync(isSeries, tvdbId, imdbId, apiKey, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(tmdbId))
            {
                return null;
            }
        }

        var key = kind + ":" + tmdbId;
        var cache = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        using var doc = await GetJsonAsync(kind + "/" + tmdbId, apiKey, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        var root = doc.RootElement;
        var info = new TmdbTitleInfo
        {
            Language = GetString(root, "original_language"),
            OriginalTitle = GetString(root, isSeries ? "original_name" : "original_title")
        };

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cache[key] = info;
            await File.WriteAllTextAsync(CachePath, JsonSerializer.Serialize(cache), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _cacheLock.Release();
        }

        return info;
    }

    private async Task<string?> FindTmdbIdAsync(
        bool isSeries,
        string? tvdbId,
        string? imdbId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        string? externalId = null;
        string? source = null;
        if (!string.IsNullOrEmpty(tvdbId))
        {
            externalId = tvdbId;
            source = "tvdb_id";
        }
        else if (!string.IsNullOrEmpty(imdbId))
        {
            externalId = imdbId;
            source = "imdb_id";
        }

        if (externalId is null)
        {
            return null;
        }

        using var doc = await GetJsonAsync("find/" + externalId + "?external_source=" + source, apiKey, cancellationToken).ConfigureAwait(false);
        if (doc is null
            || !doc.RootElement.TryGetProperty(isSeries ? "tv_results" : "movie_results", out var results)
            || results.ValueKind != JsonValueKind.Array
            || results.GetArrayLength() == 0)
        {
            return null;
        }

        return results[0].TryGetProperty("id", out var id) ? id.ToString() : null;
    }

    private async Task<JsonDocument?> GetJsonAsync(string path, string apiKey, CancellationToken cancellationToken)
    {
        // A v4 read access token is a JWT; anything else is treated as a v3 key.
        var isToken = apiKey.StartsWith("eyJ", StringComparison.Ordinal);
        var url = BaseUrl + path;
        if (!isToken)
        {
            url += (path.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "api_key=" + Uri.EscapeDataString(apiKey);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (isToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TMDb returned {Status} for {Path}", (int)response.StatusCode, path.Split('?')[0]);
            return null;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Dictionary<string, TmdbTitleInfo>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache is null)
            {
                Dictionary<string, TmdbTitleInfo>? loaded = null;
                if (File.Exists(CachePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(CachePath, cancellationToken).ConfigureAwait(false);
                        loaded = JsonSerializer.Deserialize<Dictionary<string, TmdbTitleInfo>>(json);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Ignoring unreadable TMDb cache at {Path}", CachePath);
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                _cache = loaded ?? new Dictionary<string, TmdbTitleInfo>();
            }

            return _cache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

/// <summary>
/// Original language and title as TMDb reports them.
/// </summary>
public class TmdbTitleInfo
{
    /// <summary>
    /// Gets or sets the ISO 639-1 original language.
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original title.
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;
}
