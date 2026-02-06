using Bit.Admin.AdminConsole.Controllers;
using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;

namespace Admin.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationsController))]
[SutProviderCustomize]
public class OrganizationsControllerTests
{
    #region Edit (POST)

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_NonBillableProvider_NoOp(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel { UseSecretsManager = false };

        var organization = new Organization
        {
            Id = organizationId
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Created };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        await sutProvider.GetDependency<IProviderBillingService>().DidNotReceiveWithAnyArgs()
            .ScaleSeats(Arg.Any<Provider>(), Arg.Any<PlanType>(), Arg.Any<int>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_UnmanagedOrganization_NoOp(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel { UseSecretsManager = false };

        var organization = new Organization
        {
            Id = organizationId,
            Status = OrganizationStatusType.Created
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Billable };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        await sutProvider.GetDependency<IProviderBillingService>().DidNotReceiveWithAnyArgs()
            .ScaleSeats(Arg.Any<Provider>(), Arg.Any<PlanType>(), Arg.Any<int>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_NonCBPlanType_NoOp(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();

        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            Seats = 10,
            PlanType = PlanType.FamiliesAnnually
        };

        var organization = new Organization
        {
            Id = organizationId,
            Status = OrganizationStatusType.Managed,
            Seats = 10
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Billable };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        await sutProvider.GetDependency<IProviderBillingService>().DidNotReceiveWithAnyArgs()
            .ScaleSeats(Arg.Any<Provider>(), Arg.Any<PlanType>(), Arg.Any<int>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_NoUpdateRequired_NoOp(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            Seats = 10,
            PlanType = PlanType.EnterpriseMonthly
        };

        var organization = new Organization
        {
            Id = organizationId,
            Status = OrganizationStatusType.Managed,
            Seats = 10,
            PlanType = PlanType.EnterpriseMonthly
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Billable };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        await sutProvider.GetDependency<IProviderBillingService>().DidNotReceiveWithAnyArgs()
            .ScaleSeats(Arg.Any<Provider>(), Arg.Any<PlanType>(), Arg.Any<int>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_PlanTypesUpdate_ScalesSeatsCorrectly(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            Seats = 10,
            PlanType = PlanType.EnterpriseMonthly
        };

        var organization = new Organization
        {
            Id = organizationId,
            Status = OrganizationStatusType.Managed,
            Seats = 10,
            PlanType = PlanType.TeamsMonthly
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Billable };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        var providerBillingService = sutProvider.GetDependency<IProviderBillingService>();

        await providerBillingService.Received(1).ScaleSeats(provider, organization.PlanType, -organization.Seats.Value);
        await providerBillingService.Received(1).ScaleSeats(provider, update.PlanType!.Value, organization.Seats.Value);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_SeatsUpdate_ScalesSeatsCorrectly(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            Seats = 15,
            PlanType = PlanType.EnterpriseMonthly
        };

        var organization = new Organization
        {
            Id = organizationId,
            Status = OrganizationStatusType.Managed,
            Seats = 10,
            PlanType = PlanType.EnterpriseMonthly
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Billable };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        var providerBillingService = sutProvider.GetDependency<IProviderBillingService>();

        await providerBillingService.Received(1).ScaleSeats(provider, organization.PlanType, update.Seats!.Value - organization.Seats.Value);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_ProviderSeatScaling_FullUpdate_ScalesSeatsCorrectly(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            Seats = 15,
            PlanType = PlanType.EnterpriseMonthly
        };

        var organization = new Organization
        {
            Id = organizationId,
            Status = OrganizationStatusType.Managed,
            Seats = 10,
            PlanType = PlanType.TeamsMonthly
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        var provider = new Provider { Type = ProviderType.Msp, Status = ProviderStatusType.Billable };

        sutProvider.GetDependency<IProviderRepository>().GetByOrganizationIdAsync(organizationId).Returns(provider);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        var providerBillingService = sutProvider.GetDependency<IProviderBillingService>();

        await providerBillingService.Received(1).ScaleSeats(provider, organization.PlanType, -organization.Seats.Value);
        await providerBillingService.Received(1).ScaleSeats(provider, update.PlanType!.Value, update.Seats!.Value - organization.Seats.Value + organization.Seats.Value);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_UseAutomaticUserConfirmation_FullUpdate_SavesFeatureCorrectly(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            UseAutomaticUserConfirmation = true
        };

        organization.UseAutomaticUserConfirmation = false;

        sutProvider.GetDependency<IAccessControlService>()
                .UserHasPermission(Permission.Org_Plan_Edit)
                .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        // Act
        _ = await sutProvider.Sut.Edit(organization.Id, update);

        // Assert
        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o => o.Id == organization.Id
            && o.UseAutomaticUserConfirmation == true));

        // Annul
        await organizationRepository.DeleteAsync(organization);
    }

    #endregion
}
