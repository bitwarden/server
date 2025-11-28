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

        Assert.Equal(expected, key);
    }

    [Theory, BitAutoData]
    public void BuildCacheKeyForOrganization_ReturnsExpectedKey(Guid orgId)
    {
        var expected = $"Organization:{orgId:N}";
        var key = EventIntegrationsCacheConstants.BuildCacheKeyForOrganization(orgId);

        Assert.Equal(expected, key);
    }

    [Theory, BitAutoData]
    public void BuildCacheKeyForOrganizationUser_ReturnsExpectedKey(Guid orgId, Guid userId)
    {
        var expected = $"OrganizationUserUserDetails:{orgId:N}:{userId:N}";
        var key = EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationUser(orgId, userId);

        Assert.Equal(expected, key);
    }

    [Fact]
    public void CacheName_ReturnsExpected()
    {
        Assert.Equal("EventIntegrations", EventIntegrationsCacheConstants.CacheName);
    }
}
