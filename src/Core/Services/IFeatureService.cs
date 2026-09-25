namespace Bit.Core.Services;

/// <summary>
/// Should not be used, use <see cref="Bitwarden.Server.Sdk.Features.IFeatureService"/> instead.
/// </summary>
[Obsolete("Use Bitwarden.Server.Sdk.Features.IFeatureService instead", DiagnosticId = "BWA0002")]
public interface IFeatureService
{
    /// <summary>
    /// Checks whether a given feature is enabled.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <returns>True if the feature is enabled, otherwise false.</returns>
    bool IsEnabled(string key, bool defaultValue = false);

    /// <summary>
    /// Gets the integer variation of a feature.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <returns>The feature variation value.</returns>
    int GetIntVariation(string key, int defaultValue = 0);

    /// <summary>
    /// Gets the string variation of a feature.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <returns>The feature variation value.</returns>
    string? GetStringVariation(string key, string? defaultValue = null);
}
