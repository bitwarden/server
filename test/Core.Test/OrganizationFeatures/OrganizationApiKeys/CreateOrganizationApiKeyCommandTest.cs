using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.OrganizationFeatures.OrganizationApiKeys;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationApiKeys;

[SutProviderCustomize]
public class CreateOrganizationApiKeyCommandTest
{
    [Theory]
    [BitAutoData]
    public async Task CreateAsync_CreatesOrganizationApiKey(SutProvider<CreateOrganizationApiKeyCommand> sutProvider,
        Guid organizationId, OrganizationApiKeyType keyType)
    {
        await sutProvider.Sut.CreateAsync(organizationId, keyType);

        await sutProvider.GetDependency<IOrganizationApiKeyRepository>().Received(1)
            .CreateAsync(Arg.Any<OrganizationApiKey>());
    }
}
