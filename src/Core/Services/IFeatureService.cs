using Bit.Core.Context;

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
    /// <param name="currentContext">A context providing information that can be used to evaluate whether a feature should be on or off.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <returns>True if the feature is enabled, otherwise false.</returns>
    bool IsEnabled(string key, ICurrentContext currentContext, bool defaultValue = false);

    /// <summary>
    /// Gets the integer variation of a feature.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="currentContext">A context providing information that can be used to evaluate the feature value.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <returns>The feature variation value.</returns>
    int GetIntVariation(string key, ICurrentContext currentContext, int defaultValue = 0);

    /// <summary>
    /// Gets the string variation of a feature.
    /// </summary>
    /// <param name="key">The key of the feature to check.</param>
    /// <param name="currentContext">A context providing information that can be used to evaluate the feature value.</param>
    /// <param name="defaultValue">The default value for the feature.</param>
    /// <returns>The feature variation value.</returns>
    string GetStringVariation(string key, ICurrentContext currentContext, string defaultValue = null);

    /// <summary>
    /// Gets all feature values.
    /// </summary>
    /// <param name="currentContext">A context providing information that can be used to evaluate the feature values.</param>
    /// <returns>A dictionary of feature keys and their values.</returns>
    Dictionary<string, object> GetAll(ICurrentContext currentContext);
}
