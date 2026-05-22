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
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Providers.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static Bit.Core.AdminConsole.Utilities.v2.Validation.ValidationResultHelpers;

namespace Admin.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationsController))]
[SutProviderCustomize]
public class OrganizationsControllerTests
{
    private static void StubCohortAccessAllowed(SutProvider<OrganizationsController> sutProvider)
    {
        sutProvider.GetDependency<IAccessControlService>()
            .UserHasPermission(Permission.Tools_ManagePlanMigrationCohorts)
            .Returns(true);
        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Bit.Core.FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
    }

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
    public async Task Edit_MigrationCohortAssignment_NewAssignment_CreatesAssignment(
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        cohort.MigrationPathId = null;
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohort.Id
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment)null);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id)
            .Returns(cohort);

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
                a.OrganizationId == organization.Id && a.CohortId == cohort.Id));
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
        Assert.IsType<RedirectToActionResult>(result);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_WithoutCohortPermission_SkipsAssignmentWriteButStillSavesOrg(
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Operators without Tools_ManagePlanMigrationCohorts must not be able to mutate cohort
        // assignment, even if they craft a POST that bypasses the hidden dropdown. The rest of
        // the Edit save still runs because other fields are gated on different permissions.
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohort.Id,
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Bit.Core.FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(true);
        sutProvider.GetDependency<IAccessControlService>()
            .UserHasPermission(Permission.Tools_ManagePlanMigrationCohorts)
            .Returns(false);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_WithFeatureFlagOff_SkipsAssignmentWriteButStillSavesOrg(
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        // When PM35215_BusinessPlanPriceMigration is off the dropdown is hidden, and the server
        // must refuse cohort writes even from a crafted POST that includes the field.
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohort.Id,
        };

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(Bit.Core.FeatureFlagKeys.PM35215_BusinessPlanPriceMigration)
            .Returns(false);
        sutProvider.GetDependency<IAccessControlService>()
            .UserHasPermission(Permission.Tools_ManagePlanMigrationCohorts)
            .Returns(true);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_UnchangedCohort_SkipsRepository(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var cohortId = Guid.NewGuid();
        var existing = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId,
        };

        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohortId
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(existing);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        // The early-skip on equal cohort id must also avoid the unnecessary cohort lookup.
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    public enum LifecycleColumn { Scheduled, Migrated, ChurnDiscountApplied }

    public static TheoryData<LifecycleColumn> LockedLifecycleColumns =>
        new()
        {
            LifecycleColumn.Scheduled,
            LifecycleColumn.Migrated,
            LifecycleColumn.ChurnDiscountApplied,
        };

    [Theory]
    [BitMemberAutoData(nameof(LockedLifecycleColumns))]
    public async Task Edit_MigrationCohortAssignment_LockedReassignment_SurfacesConflictAndSkipsReplace(
        LifecycleColumn lockedColumn,
        Organization organization,
        Guid newCohortId,
        SutProvider<OrganizationsController> sutProvider)
    {
        // The view disables the <select> client-side, but that's just a UX hint;
        // the server is the only place this invariant lives.
        var locked = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        switch (lockedColumn)
        {
            case LifecycleColumn.Scheduled:
                locked.ScheduledDate = DateTime.UtcNow;
                break;
            case LifecycleColumn.Migrated:
                locked.MigratedDate = DateTime.UtcNow;
                break;
            case LifecycleColumn.ChurnDiscountApplied:
                locked.ChurnDiscountAppliedDate = DateTime.UtcNow;
                break;
        }
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = newCohortId,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(locked);

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(
            "This organization's migration cohort is locked because its assignment has already entered the migration pipeline.",
            sutProvider.Sut.TempData["Error"]);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_Locked_SameCohortSubmitted_AllowsSave(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // When the cohort dropdown is locked the view backs the disabled <select> with a
        // hidden input that round-trips the existing cohort id, so locked orgs must be able
        // to save edits to other fields without tripping the lock guard.
        var cohortId = Guid.NewGuid();
        var locked = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = cohortId,
            MigratedDate = DateTime.UtcNow,
        };
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohortId,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(locked);

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(sutProvider.Sut.TempData["Error"]);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_MissingTargetCohort_SurfacesBadRequestAndSkipsReplace(
        Organization organization,
        Guid newCohortId,
        SutProvider<OrganizationsController> sutProvider)
    {
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = newCohortId,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment)null);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(newCohortId)
            .Returns((OrganizationPlanMigrationCohort)null);

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal("The selected migration cohort no longer exists.", sutProvider.Sut.TempData["Error"]);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceive().ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_Unassign_DeletesAssignment(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var existing = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = null,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(existing);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .Received(1)
            .DeleteAsync(existing);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_Move_DeletesThenCreates(
        Organization organization,
        OrganizationPlanMigrationCohort targetCohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        targetCohort.MigrationPathId = null;
        var existing = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = targetCohort.Id,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(targetCohort.Id)
            .Returns(targetCohort);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        var assignmentRepo = sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>();
        // UNIQUE(OrganizationId) at the DB layer forces delete before create; the test pins that order.
        // The created row must be a fresh entity, not an in-place mutation of the existing assignment.
        Received.InOrder(() =>
        {
            assignmentRepo.DeleteAsync(existing);
            assignmentRepo.CreateAsync(Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
                a.OrganizationId == organization.Id
                && a.CohortId == targetCohort.Id
                && a.Id != existing.Id));
        });
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    public static TheoryData<Exception> CohortWriteFailureExceptions =>
        new()
        {
            new DbUpdateException("simulated repo failure"),
            new TimeoutException("simulated timeout"),
            new InvalidOperationException("simulated non-listed failure"),
        };

    [Theory]
    [BitMemberAutoData(nameof(CohortWriteFailureExceptions))]
    public async Task Edit_MigrationCohortAssignment_NewAssignment_CreateThrows_SurfacesWarningButOrgIsSaved(
        Exception thrown,
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        // The cohort write runs after ReplaceAsync. A failure here must not block the org-level
        // save; the operator sees a warning and is told to retry the cohort change. Any exception
        // surfaces the same way -- the catch must not be narrow enough to leak a 500.
        cohort.MigrationPathId = null;
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohort.Id,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment)null);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id)
            .Returns(cohort);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .CreateAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>())
            .ThrowsAsync(thrown);

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(
            "Organization updated successfully, but the migration cohort assignment could not be saved. Reload this page and retry.",
            sutProvider.Sut.TempData["Warning"]);
        Assert.Null(sutProvider.Sut.TempData["Error"]);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
        // Downstream pipeline must still run after a cohort write failure -- the ability cache
        // would otherwise be stale relative to the just-replaced organization.
        await sutProvider.GetDependency<IApplicationCacheService>().Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(o => o.Id == organization.Id));
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_Move_CreateThrowsAfterDelete_SurfacesWarningButOrgIsSaved(
        Organization organization,
        OrganizationPlanMigrationCohort targetCohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        targetCohort.MigrationPathId = null;
        var existing = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = targetCohort.Id,
        };

        var assignmentRepo = sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>();
        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        assignmentRepo.GetByOrganizationIdAsync(organization.Id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(targetCohort.Id)
            .Returns(targetCohort);
        assignmentRepo
            .CreateAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>())
            .ThrowsAsync(new DbUpdateException("simulated repo failure"));

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(
            "Organization updated successfully, but the migration cohort assignment could not be saved. Reload this page and retry.",
            sutProvider.Sut.TempData["Warning"]);
        // UNIQUE(OrganizationId) at the DB layer forces delete before create on a move; pin the order.
        // On failure here the row is left unassigned and the operator recovers by re-submitting Save.
        await assignmentRepo.Received(1).DeleteAsync(existing);
        await assignmentRepo.Received(1).CreateAsync(Arg.Is<OrganizationPlanMigrationCohortAssignment>(a =>
            a.OrganizationId == organization.Id && a.CohortId == targetCohort.Id));
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_Move_DeleteThrows_SurfacesWarningButOrgIsSaved(
        Organization organization,
        OrganizationPlanMigrationCohort targetCohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Symmetric to the CreateThrowsAfterDelete case -- the broad catch must also swallow a
        // delete-side failure, leaving the prior assignment intact and surfacing the same warning.
        targetCohort.MigrationPathId = null;
        var existing = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = targetCohort.Id,
        };

        var assignmentRepo = sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>();
        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        assignmentRepo.GetByOrganizationIdAsync(organization.Id).Returns(existing);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(targetCohort.Id)
            .Returns(targetCohort);
        assignmentRepo
            .DeleteAsync(Arg.Any<OrganizationPlanMigrationCohortAssignment>())
            .ThrowsAsync(new DbUpdateException("simulated repo failure"));

        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(
            "Organization updated successfully, but the migration cohort assignment could not be saved. Reload this page and retry.",
            sutProvider.Sut.TempData["Warning"]);
        await assignmentRepo.Received(1).DeleteAsync(existing);
        // The catch swallows the delete failure before any insert is attempted -- the prior row
        // is preserved by the failed delete and no new row is inserted.
        await assignmentRepo.DidNotReceiveWithAnyArgs().CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_GuidEmptySubmittedFromUnassigned_DoesNotLookUpCohort(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Form binding can surface "(Not assigned)" as Guid.Empty; the controller normalizes it
        // to null so the unassigned-to-unassigned diff produces no repository writes and no
        // bogus GetByIdAsync(Guid.Empty) lookup.
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = Guid.Empty,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment)null);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_GuidEmptySubmittedFromAssigned_UnassignsOrg(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Guid.Empty must be treated as "(Not assigned)" -- the unassign path runs, not a
        // bogus assignment to a non-existent cohort.
        var existing = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = Guid.Empty,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(existing);

        _ = await sutProvider.Sut.Edit(organization.Id, update);

        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .Received(1)
            .DeleteAsync(existing);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>().Received(1).ReplaceAsync(Arg.Any<Organization>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_ModelStateInvalid_SkipsCohortHandling(
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        // If unrelated model validation fails the controller short-circuits before any
        // persistence; the cohort block must not have run. Pins ordering so a future refactor
        // that moves the cohort block above the ModelState check would fail this test.
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.TeamsMonthly,
            MigrationCohortId = cohort.Id,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.Sut.ModelState.AddModelError("Name", "Name is required");
        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        Assert.IsType<RedirectToActionResult>(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .ReplaceAsync(Arg.Any<Organization>());
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

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_MigrationCohortAssignment_IncompatiblePlan_SurfacesErrorAndSkipsWrite(
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Defense-in-depth: even if a stale dropdown or crafted POST submits a cohort whose
        // MigrationPath.FromPlan differs from the org's PlanType, the controller must reject the
        // write and surface an error instead of persisting a mismatched assignment.
        organization.PlanType = PlanType.EnterpriseAnnually2020;
        cohort.MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent;
        var update = new OrganizationEditModel
        {
            PlanType = PlanType.EnterpriseAnnually2020,
            MigrationCohortId = cohort.Id,
        };

        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns((OrganizationPlanMigrationCohortAssignment)null);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id)
            .Returns(cohort);
        sutProvider.Sut.TempData = new TempDataDictionary(new DefaultHttpContext(), Substitute.For<ITempDataProvider>());

        var result = await sutProvider.Sut.Edit(organization.Id, update);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        Assert.Equal(
            "The selected migration cohort is not compatible with this organization's plan.",
            sutProvider.Sut.TempData["Error"]);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .CreateAsync(default);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .DeleteAsync(default);
    }

    #endregion

    #region Edit (GET)

    private static void StubEditGetDependencies(
        SutProvider<OrganizationsController> sutProvider,
        Organization organization,
        OrganizationPlanMigrationCohortAssignment currentAssignment,
        IReadOnlyList<OrganizationPlanMigrationCohort> availableCohorts = null)
    {
        StubCohortAccessAllowed(sutProvider);
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id)
            .Returns(currentAssignment);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetManyAsync()
            .Returns(availableCohorts ?? Array.Empty<OrganizationPlanMigrationCohort>());
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_Unassigned_RendersUnlockedDropdown(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // The view binds the dropdown items off AvailableMigrationCohorts; the GET must surface
        // the repository result on the model so the dropdown is populated.
        OrganizationPlanMigrationCohort[] availableCohorts =
        [
            new() { Id = Guid.NewGuid(), Name = "Alpha" },
            new() { Id = Guid.NewGuid(), Name = "Beta" },
        ];
        StubEditGetDependencies(sutProvider, organization, currentAssignment: null, availableCohorts);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.Null(model.MigrationCohortId);
        Assert.False(model.MigrationCohortLocked);
        Assert.Null(model.MigrationCohortLockReason);
        Assert.NotNull(model.AvailableMigrationCohorts);
        Assert.Equal(availableCohorts.Select(c => c.Id), model.AvailableMigrationCohorts.Select(c => c.Id));
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_AssignedButPending_RendersUnlockedDropdown(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
        };
        StubEditGetDependencies(sutProvider, organization, assignment);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.Equal(assignment.CohortId, model.MigrationCohortId);
        Assert.False(model.MigrationCohortLocked);
        Assert.Null(model.MigrationCohortLockReason);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_Scheduled_LocksDropdownWithScheduledReason(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow,
        };
        StubEditGetDependencies(sutProvider, organization, assignment);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.True(model.MigrationCohortLocked);
        Assert.Equal("Locked: a migration has already been scheduled for this organization.",
            model.MigrationCohortLockReason);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_Migrated_LocksDropdownWithMigratedReason(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
            MigratedDate = DateTime.UtcNow,
        };
        StubEditGetDependencies(sutProvider, organization, assignment);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.True(model.MigrationCohortLocked);
        Assert.Equal("Locked: this organization has already been migrated.",
            model.MigrationCohortLockReason);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_ChurnMitigated_LocksDropdownWithChurnReason(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
            ChurnDiscountAppliedDate = DateTime.UtcNow,
        };
        StubEditGetDependencies(sutProvider, organization, assignment);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.True(model.MigrationCohortLocked);
        Assert.Equal("Locked: a churn-mitigation discount has already been applied to this organization.",
            model.MigrationCohortLockReason);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_AllLifecycleDatesSet_PrefersMigratedReason(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Precedence is operator-facing: Migrated wins over Scheduled and ChurnDiscountApplied.
        // Pin the order so a refactor of the switch arms can't silently change the reason.
        var assignment = new OrganizationPlanMigrationCohortAssignment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            CohortId = Guid.NewGuid(),
            ScheduledDate = DateTime.UtcNow,
            MigratedDate = DateTime.UtcNow,
            ChurnDiscountAppliedDate = DateTime.UtcNow,
        };
        StubEditGetDependencies(sutProvider, organization, assignment);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.True(model.MigrationCohortLocked);
        Assert.Equal("Locked: this organization has already been migrated.",
            model.MigrationCohortLockReason);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_WithoutCohortPermissionOrFlag_LeavesDropdownHidden(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // With FF off or the cohort permission absent, the GET must not surface dropdown data --
        // a null AvailableMigrationCohorts is the signal the view uses to hide the row entirely.
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.Null(model.AvailableMigrationCohorts);
        Assert.Null(model.MigrationCohortId);
        Assert.False(model.MigrationCohortLocked);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetManyAsync();
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByOrganizationIdAsync(default);
    }

    [BitAutoData]
    [SutProviderCustomize]
    [Theory]
    public async Task Edit_Get_FiltersCohortsByOrgPlan(
        Organization organization,
        SutProvider<OrganizationsController> sutProvider)
    {
        // Operators must not be offered cohorts whose MigrationPath.FromPlan differs from this
        // org's PlanType; cohorts without a path are universally visible (e.g. pre-staged rows).
        organization.PlanType = PlanType.EnterpriseAnnually2020;
        var matching = new OrganizationPlanMigrationCohort
        {
            Id = Guid.NewGuid(),
            Name = "Matches",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
        };
        var mismatched = new OrganizationPlanMigrationCohort
        {
            Id = Guid.NewGuid(),
            Name = "Mismatches",
            MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent,
        };
        var pathless = new OrganizationPlanMigrationCohort
        {
            Id = Guid.NewGuid(),
            Name = "Pathless",
            MigrationPathId = null,
        };
        StubEditGetDependencies(sutProvider, organization, currentAssignment: null,
            availableCohorts: [matching, mismatched, pathless]);

        var result = await sutProvider.Sut.Edit(organization.Id);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OrganizationEditModel>(view.Model);
        Assert.NotNull(model.AvailableMigrationCohorts);
        Assert.Equal(
            new[] { matching.Id, pathless.Id },
            model.AvailableMigrationCohorts.Select(c => c.Id));
    }

    #endregion
}
