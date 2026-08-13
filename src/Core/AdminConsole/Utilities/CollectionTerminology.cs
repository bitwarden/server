using Bit.Core.Services;

namespace Bit.Core.AdminConsole.Utilities;

/// <summary>
/// Centralizes the "collections" vs. "shared folders" wording used in user-facing collection-limit
/// error messages. Term selection is gated behind the <see cref="FeatureFlagKeys.VFO1Foundation"/>
/// feature flag so all callers stay in sync as the flag rolls out (and can be cleaned up from a
/// single place once the flag is removed).
/// </summary>
public static class CollectionTerminology
{
    private const string Collections = "collections";
    private const string SharedFolders = "shared folders";

    /// <summary>
    /// Returns "shared folders" when the <see cref="FeatureFlagKeys.VFO1Foundation"/> feature flag is
    /// enabled, otherwise "collections".
    /// </summary>
    public static string Plural(IFeatureService featureService) =>
        Plural(featureService.IsEnabled(FeatureFlagKeys.VFO1Foundation));

    /// <summary>
    /// Returns "shared folders" when <paramref name="useSharedFolderTerminology"/> is <c>true</c>,
    /// otherwise "collections". Use this overload for callers (e.g. models) that do not have access to
    /// <see cref="IFeatureService"/> and must have the flag's value passed in by the caller.
    /// </summary>
    public static string Plural(bool useSharedFolderTerminology) =>
        useSharedFolderTerminology ? SharedFolders : Collections;
}
