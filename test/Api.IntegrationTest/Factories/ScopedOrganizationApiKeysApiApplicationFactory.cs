using Bit.Core;

namespace Bit.Api.IntegrationTest.Factories;

/// <summary>
/// An <see cref="ApiApplicationFactory"/> with the scoped organization API keys feature flag on in Api and Identity.
/// </summary>
public class ScopedOrganizationApiKeysApiApplicationFactory : ApiApplicationFactory
{
    private const string FlagSettingKey =
        $"globalSettings:launchDarkly:flagValues:{FeatureFlagKeys.ScopedOrganizationApiKeys}";

    public ScopedOrganizationApiKeysApiApplicationFactory()
    {
        UpdateConfiguration(FlagSettingKey, "true");
        Identity.UpdateConfiguration(FlagSettingKey, "true");
    }
}
