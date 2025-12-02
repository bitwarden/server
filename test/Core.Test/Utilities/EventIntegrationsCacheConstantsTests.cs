using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class EventIntegrationsCacheConstantsTests
{
    [Theory, BitAutoData]
    public void BuildCacheKeyForGroup_ReturnsExpectedKey(Guid groupId)
    {
        var expected = $"Group:{groupId:N}";
        var key = EventIntegrationsCacheConstants.BuildCacheKeyForGroup(groupId);
        var keyWithDifferentGroup = EventIntegrationsCacheConstants.BuildCacheKeyForGroup(Guid.NewGuid());
        var keyWithSameGroup = EventIntegrationsCacheConstants.BuildCacheKeyForGroup(groupId);

        Assert.Equal(expected, key);
        Assert.NotEqual(key, keyWithDifferentGroup);
        Assert.Equal(key, keyWithSameGroup);
    }

    [Theory, BitAutoData]
    public void BuildCacheKeyForOrganization_ReturnsExpectedKey(Guid orgId)
    {
        var expected = $"Organization:{orgId:N}";
        var key = EventIntegrationsCacheConstants.BuildCacheKeyForOrganization(orgId);
        var keyWithDifferentOrg = EventIntegrationsCacheConstants.BuildCacheKeyForOrganization(Guid.NewGuid());
        var keyWithSameOrg = EventIntegrationsCacheConstants.BuildCacheKeyForOrganization(orgId);

        Assert.Equal(expected, key);
        Assert.NotEqual(key, keyWithDifferentOrg);
        Assert.Equal(key, keyWithSameOrg);
    }

    [Theory, BitAutoData]
    public void BuildCacheKeyForOrganizationIntegrationConfigurationDetails_ReturnsExpectedKey(Guid orgId)
    {
        var integrationType = IntegrationType.Hec;

        var expectedWithEvent = $"OrganizationIntegrationConfigurationDetails:{orgId:N}:Hec:User_LoggedIn";
        var keyWithEvent = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            orgId, integrationType, EventType.User_LoggedIn);
        var keyWithDifferentEvent = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            orgId, integrationType, EventType.Cipher_Created);
        var keyWithDifferentIntegration = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            orgId, IntegrationType.Webhook, EventType.User_LoggedIn);
        var keyWithDifferentOrganization = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            Guid.NewGuid(), integrationType, EventType.User_LoggedIn);
        var keyWithSameDetails = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            orgId, integrationType, EventType.User_LoggedIn);

        Assert.Equal(expectedWithEvent, keyWithEvent);
        Assert.NotEqual(keyWithEvent, keyWithDifferentEvent);
        Assert.NotEqual(keyWithEvent, keyWithDifferentIntegration);
        Assert.NotEqual(keyWithEvent, keyWithDifferentOrganization);
        Assert.Equal(keyWithEvent, keyWithSameDetails);

        var expectedWithNullEvent = $"OrganizationIntegrationConfigurationDetails:{orgId:N}:Hec:";
        var keyWithNullEvent = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            orgId, integrationType, null);
        var keyWithNullEventDifferentIntegration = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            orgId, IntegrationType.Webhook, null);
        var keyWithNullEventDifferentOrganization = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
            Guid.NewGuid(), integrationType, null);

        Assert.Equal(expectedWithNullEvent, keyWithNullEvent);
        Assert.NotEqual(keyWithEvent, keyWithNullEvent);
        Assert.NotEqual(keyWithNullEvent, keyWithDifferentEvent);
        Assert.NotEqual(keyWithNullEvent, keyWithNullEventDifferentIntegration);
        Assert.NotEqual(keyWithNullEvent, keyWithNullEventDifferentOrganization);
    }

    [Theory, BitAutoData]
    public void BuildCacheTagForOrganizationIntegration_ReturnsExpectedKey(Guid orgId)
    {
        var expected = $"OrganizationIntegration:{orgId:N}:Hec";
        var tag = EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            orgId, IntegrationType.Hec);
        var tagWithDifferentOrganization = EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            Guid.NewGuid(), IntegrationType.Hec);
        var tagWithDifferentIntegrationType = EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            orgId, IntegrationType.Webhook);
        var tagWithSameDetails = EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
            orgId, IntegrationType.Hec);

        Assert.Equal(expected, tag);
        Assert.NotEqual(tag, tagWithDifferentOrganization);
        Assert.NotEqual(tag, tagWithDifferentIntegrationType);
        Assert.Equal(tag, tagWithSameDetails);
    }

    [Theory, BitAutoData]
    public void BuildCacheKeyForOrganizationUser_ReturnsExpectedKey(Guid orgId, Guid userId)
    {
        var expected = $"OrganizationUserUserDetails:{orgId:N}:{userId:N}";
        var key = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationUser(orgId, userId);
        var keyWithDifferentOrg = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationUser(Guid.NewGuid(), userId);
        var keyWithDifferentUser = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationUser(orgId, Guid.NewGuid());
        var keyWithSameDetails = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationUser(orgId, userId);

        Assert.Equal(expected, key);
        Assert.NotEqual(key, keyWithDifferentOrg);
        Assert.NotEqual(key, keyWithDifferentUser);
        Assert.Equal(key, keyWithSameDetails);
    }

    [Fact]
    public void CacheName_ReturnsExpected()
    {
        Assert.Equal("EventIntegrations", EventIntegrationsCacheConstants.CacheName);
    }

    [Fact]
    public void DurationForOrganizationIntegrationConfigurationDetails_ReturnsExpected()
    {
        Assert.Equal(
            TimeSpan.FromDays(1),
            EventIntegrationsCacheConstants.DurationForOrganizationIntegrationConfigurationDetails
        );
    }
}
