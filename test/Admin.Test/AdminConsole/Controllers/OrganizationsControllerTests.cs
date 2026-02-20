using Bit.Admin.AdminConsole.Controllers;
using Bit.Admin.AdminConsole.Models;
using Bit.Admin.Enums;
using Bit.Admin.Services;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

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

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organization.Id);
        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Valid(request));

        // Act
        _ = await sutProvider.Sut.Edit(organization.Id, update);

        // Assert
        await organizationRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(o => o.Id == organization.Id
            && o.UseAutomaticUserConfirmation == true));
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_EnableUseAutomaticUserConfirmation_ValidationFails_RedirectsWithError(
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

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organization.Id);
        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Invalid(request, new UserNotCompliantWithSingleOrganization()));

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        // Act
        var result = await sutProvider.Sut.Edit(organization.Id, update);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirectResult.ActionName);
        Assert.Equal(organization.Id, redirectResult.RouteValues!["id"]);

        await organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_EnableUseAutomaticUserConfirmation_ProviderValidationFails_RedirectsWithError(
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

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organization.Id);
        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Invalid(request, new ProviderExistsInOrganization()));

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        // Act
        var result = await sutProvider.Sut.Edit(organization.Id, update);

        // Assert
        var redirectResult = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirectResult.ActionName);

        await organizationRepository.DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_UseAutomaticUserConfirmation_NotChanged_DoesNotCallValidator(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            UseAutomaticUserConfirmation = false
        };

        var organization = new Organization
        {
            Id = organizationId,
            UseAutomaticUserConfirmation = false
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        await sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .DidNotReceive()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_UseAutomaticUserConfirmation_AlreadyEnabled_DoesNotCallValidator(
        SutProvider<OrganizationsController> sutProvider)
    {
        // Arrange
        var organizationId = new Guid();
        var update = new OrganizationEditModel
        {
            UseSecretsManager = false,
            UseAutomaticUserConfirmation = true
        };

        var organization = new Organization
        {
            Id = organizationId,
            UseAutomaticUserConfirmation = true
        };

        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organizationId)
            .Returns(organization);

        // Act
        _ = await sutProvider.Sut.Edit(organizationId, update);

        // Assert
        await sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .DidNotReceive()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_UseAutomaticUserConfirmation_EnabledByPortal_LogsEvent(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            UseAutomaticUserConfirmation = true
        };

        organization.UseAutomaticUserConfirmation = false;
        organization.Enabled = true;
        organization.UseEvents = true;

        sutProvider.GetDependency<IAccessControlService>()
                .UserHasPermission(Permission.Org_Plan_Edit)
                .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        var request = new AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest(organization.Id);
        sutProvider.GetDependency<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator>()
            .IsOrganizationCompliantAsync(Arg.Any<AutomaticUserConfirmationOrganizationPolicyComplianceValidatorRequest>())
            .Returns(Valid(request));

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationEventAsync(
                Arg.Is<Organization>(o => o.Id == organization.Id),
                EventType.Organization_AutoConfirmEnabled_Portal,
                EventSystemUser.BitwardenPortal);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_UseAutomaticUserConfirmation_DisabledByPortal_LogsEvent(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            UseAutomaticUserConfirmation = false
        };

        organization.UseAutomaticUserConfirmation = true;
        organization.Enabled = true;
        organization.UseEvents = true;

        sutProvider.GetDependency<IAccessControlService>()
                .UserHasPermission(Permission.Org_Plan_Edit)
                .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationEventAsync(
                Arg.Is<Organization>(o => o.Id == organization.Id),
                EventType.Organization_AutoConfirmDisabled_Portal,
                EventSystemUser.BitwardenPortal);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_UseAutomaticUserConfirmation_NoChange_DoesNotLogEvent(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            UseAutomaticUserConfirmation = true
        };

        organization.UseAutomaticUserConfirmation = true;
        organization.Enabled = true;
        organization.UseEvents = true;

        sutProvider.GetDependency<IAccessControlService>()
                .UserHasPermission(Permission.Org_Plan_Edit)
                .Returns(true);

        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IEventService>().DidNotReceive()
            .LogOrganizationEventAsync(
                Arg.Any<Organization>(),
                Arg.Any<EventType>(),
                Arg.Any<EventSystemUser>());
    }

    #endregion
}
