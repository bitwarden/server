using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class OrganizationEnableCommandTests
{
    [Theory, BitAutoData]
    public async Task EnableAsync_WhenOrganizationDoesNotExist_DoesNothing(
        Guid organizationId,
        SutProvider<OrganizationEnableCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        await sutProvider.Sut.EnableAsync(organizationId);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .UpsertOrganizationAbilityAsync(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_WhenOrganizationAlreadyEnabled_DoesNothing(
        Organization organization,
        SutProvider<OrganizationEnableCommand> sutProvider)
    {
        organization.Enabled = true;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.EnableAsync(organization.Id);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .UpsertOrganizationAbilityAsync(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_WhenOrganizationDisabled_EnablesAndSaves(
        Organization organization,
        SutProvider<OrganizationEnableCommand> sutProvider)
    {
        organization.Enabled = false;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.EnableAsync(organization.Id);

        Assert.True(organization.Enabled);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .ReplaceAsync(organization);
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_WithExpiration_WhenOrganizationHasNoGateway_DoesNothing(
        Organization organization,
        DateTime expirationDate,
        SutProvider<OrganizationEnableCommand> sutProvider)
    {
        organization.Enabled = false;
        organization.Gateway = null;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.EnableAsync(organization.Id, expirationDate);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
        await sutProvider.GetDependency<IApplicationCacheService>()
            .DidNotReceive()
            .UpsertOrganizationAbilityAsync(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_WithExpiration_WhenValid_EnablesAndSetsExpiration(
        Organization organization,
        DateTime expirationDate,
        SutProvider<OrganizationEnableCommand> sutProvider)
    {
        organization.Enabled = false;
        organization.Gateway = GatewayType.Stripe;
        organization.RevisionDate = DateTime.UtcNow.AddDays(-1);
        var originalRevisionDate = organization.RevisionDate;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.EnableAsync(organization.Id, expirationDate);

        Assert.True(organization.Enabled);
        Assert.Equal(expirationDate, organization.ExpirationDate);
        Assert.True(organization.RevisionDate > originalRevisionDate);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .ReplaceAsync(organization);
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
    }

    [Theory, BitAutoData]
    public async Task EnableAsync_WithoutExpiration_DoesNotUpdateRevisionDate(
        Organization organization,
        SutProvider<OrganizationEnableCommand> sutProvider)
    {
        organization.Enabled = false;
        var originalRevisionDate = organization.RevisionDate;
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        await sutProvider.Sut.EnableAsync(organization.Id);

        Assert.True(organization.Enabled);
        Assert.Equal(originalRevisionDate, organization.RevisionDate);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .ReplaceAsync(organization);
        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(organization);
    }
}
