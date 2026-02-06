using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationDisableCommandTests
{
    [Theory, BitAutoData]
    public async Task DisableAsync_WhenOrganizationEnabled_DisablesSuccessfully(
        Organization organization,
        DateTime expirationDate,
        SutProvider<OrganizationDisableCommand> sutProvider)
    {
        organization.Enabled = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.DisableAsync(organization.Id, expirationDate);

        Assert.False(organization.Enabled);
        Assert.Equal(expirationDate, organization.ExpirationDate);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .ReplaceAsync(organization);
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task DisableAsync_WhenOrganizationNotFound_DoesNothing(
        Guid organizationId,
        DateTime expirationDate,
        SutProvider<OrganizationDisableCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        await sutProvider.Sut.DisableAsync(organizationId, expirationDate);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .UpsertOrganizationAbilityAsync(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task DisableAsync_WhenOrganizationAlreadyDisabled_DoesNothing(
        Organization organization,
        DateTime expirationDate,
        SutProvider<OrganizationDisableCommand> sutProvider)
    {
        organization.Enabled = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.DisableAsync(organization.Id, expirationDate);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .UpsertOrganizationAbilityAsync(Arg.Any<Organization>());
    }
}
