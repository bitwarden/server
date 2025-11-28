using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationApiKeys;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationApiKeys;

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
            .CreateAsync(Arg.Is<OrganizationApiKey>(o => o.OrganizationId == organizationId
                                                         && o.Type == keyType));
    }
}
