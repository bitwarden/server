using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationApiKeys;

[SutProviderCustomize]
public class RotateOrganizationApiKeyCommandTests
{
    [Theory, BitAutoData]
    public async Task RotateApiKeyAsync_RotatesKey(
        SutProvider<RotateOrganizationApiKeyCommand> sutProvider,
        OrganizationApiKey organizationApiKey
    )
    {
        var existingKey = organizationApiKey.ApiKey;
        organizationApiKey = await sutProvider.Sut.RotateApiKeyAsync(organizationApiKey);
        Assert.NotEqual(existingKey, organizationApiKey.ApiKey);
        AssertHelper.AssertRecent(organizationApiKey.RevisionDate);
    }
}
