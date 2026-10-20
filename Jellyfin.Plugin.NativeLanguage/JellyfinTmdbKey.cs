using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NativeLanguage;

/// <summary>
/// Borrows the TMDb key Jellyfin itself uses: the one set in the TMDb plugin's settings, else Jellyfin's built-in key.
/// </summary>
internal static class JellyfinTmdbKey
{
    private const string ProvidersAssembly = "MediaBrowser.Providers";
    private const string TmdbPluginType = "MediaBrowser.Providers.Plugins.Tmdb.Plugin";
    private const string TmdbUtilsType = "MediaBrowser.Providers.Plugins.Tmdb.TmdbUtils";

    /// <summary>
    /// Finds the key, or returns null if Jellyfin's internals have changed.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <returns>The key, or null.</returns>
    public static string? Find(ILogger logger)
    {
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == ProvidersAssembly);
            if (assembly is null)
            {
                logger.LogWarning("Native Language: {Assembly} is not loaded, so Jellyfin's TMDb key can't be used", ProvidersAssembly);
                return null;
            }

            // Same order as Jellyfin's own TMDb client: the admin's key wins over the built-in one.
            var instance = assembly.GetType(TmdbPluginType)
                ?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            var configuration = instance?.GetType().GetProperty("Configuration")?.GetValue(instance);
            if (configuration?.GetType().GetProperty("TmdbApiKey")?.GetValue(configuration) is string custom
                && !string.IsNullOrWhiteSpace(custom))
            {
                return custom.Trim();
            }

            var field = assembly.GetType(TmdbUtilsType)
                ?.GetField("ApiKey", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var builtIn = field is null ? null : (field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null)) as string;
            if (string.IsNullOrWhiteSpace(builtIn))
            {
                logger.LogWarning("Native Language: Jellyfin's built-in TMDb key was not found; set a key in the plugin settings");
                return null;
            }

            return builtIn;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Native Language: could not read Jellyfin's TMDb key; set a key in the plugin settings");
            return null;
        }
    }
}
