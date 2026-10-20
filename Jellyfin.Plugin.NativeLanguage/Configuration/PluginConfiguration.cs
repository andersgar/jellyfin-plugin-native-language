using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NativeLanguage.Configuration;

/// <summary>
/// Which original languages get their original title.
/// </summary>
public enum LanguageMode
{
    /// <summary>
    /// Only the listed languages, e.g. <c>sv</c> so Swedish films keep their Swedish title.
    /// </summary>
    OnlyListed,

    /// <summary>
    /// All languages except the listed ones, e.g. <c>ja,ko,zh</c> to skip titles in other scripts.
    /// </summary>
    AllExceptListed
}

/// <summary>
/// Plugin settings, edited on the plugin's dashboard page.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether the plugin changes titles at all.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets an own TMDb API key (v3 key or v4 read access token). Empty uses Jellyfin's TMDb key.
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how <see cref="Languages"/> is used.
    /// </summary>
    public LanguageMode Mode { get; set; } = LanguageMode.OnlyListed;

    /// <summary>
    /// Gets or sets the original languages (ISO 639-1, comma separated).
    /// </summary>
    public string Languages { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether movies are handled.
    /// </summary>
    public bool IncludeMovies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether series are handled.
    /// </summary>
    public bool IncludeSeries { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the title is locked so metadata refreshes keep it.
    /// </summary>
    public bool LockName { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether changes are only logged, not saved.
    /// </summary>
    public bool DryRun { get; set; }
}
