using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements;

[SutProviderCustomize]
public class OrganizationEnableCollectionEnhancementsCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task EnableCollectionEnhancements_Success(
        SutProvider<OrganizationEnableCollectionEnhancementsCommand> sutProvider,
        Organization organization)
    {
        await sutProvider.Sut.EnableCollectionEnhancements(organization);

        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).EnableCollectionEnhancements(organization.Id);
        await sutProvider.GetDependency<IOrganizationService>().Received(1).ReplaceAndUpdateCacheAsync(
            Arg.Is<Organization>(o =>
                o.Id == organization.Id &&
                o.FlexibleCollections));
    }
}
