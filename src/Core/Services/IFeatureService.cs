namespace Bit.Core.Services;

public interface IFeatureService
{
    /// <summary>
    /// Checks whether online access to feature status is available.
    /// </summary>
    /// <returns>True if the service is online, otherwise false.</returns>
    bool IsOnline();

    /// <summary>
    /// Checks whether a given feature is enabled.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <param name="overridingOrganizationId">Sole overriding organization ID, used with context assessment.</param>
    /// <returns>True if the feature is enabled, otherwise false.</returns>
    bool IsEnabled(string key, bool defaultValue = false, Guid? overridingOrganizationId = null);

    /// <summary>
    /// Gets the integer variation of a feature.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <param name="overridingOrganizationId">Sole overriding organization ID, used with context assessment.</param>
    /// <returns>The feature variation value.</returns>
    int GetIntVariation(string key, int defaultValue = 0, Guid? overridingOrganizationId = null);

    /// <summary>
    /// Gets the string variation of a feature.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <param name="overridingOrganizationId">Sole overriding organization ID, used with context assessment.</param>
    /// <returns>The feature variation value.</returns>
    string GetStringVariation(string key, string defaultValue = null, Guid? overridingOrganizationId = null);

    /// <summary>
    /// Gets all feature values.
    /// </summary>
    /// <param name="overridingOrganizationId">Sole overriding organization ID, used with context assessment.</param>
    /// <returns>A dictionary of feature keys and their values.</returns>
    Dictionary<string, object> GetAll(Guid? overridingOrganizationId = null);
}
