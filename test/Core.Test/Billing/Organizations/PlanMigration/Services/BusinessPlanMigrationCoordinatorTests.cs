using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.Services;
using Bit.Core.Billing.Pricing;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Stripe;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Services;

[SutProviderCustomize]
public class BusinessPlanMigrationCoordinatorTests
{
    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenNoAssignment_ReturnsNotAssigned(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns((OrganizationPlanMigrationCohortAssignment?)null);

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.NotAssigned, outcome);
        await sutProvider.GetDependency<IPriceIncreaseScheduler>()
            .DidNotReceiveWithAnyArgs().ScheduleForSubscription(default!);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenAlreadyMigrated_ReturnsAlreadyMigrated(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment assignment)
    {
        assignment.MigratedDate = DateTime.UtcNow;
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(assignment);

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.AlreadyMigrated, outcome);
        await sutProvider.GetDependency<IPriceIncreaseScheduler>()
            .DidNotReceiveWithAnyArgs().ScheduleForSubscription(default!);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenSchedulerDeclines_ReturnsNotScheduled(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment assignment)
    {
        assignment.MigratedDate = null;
        assignment.ScheduledDate = null;
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        sutProvider.GetDependency<IPriceIncreaseScheduler>()
            .ScheduleForSubscription(Arg.Any<Subscription>()).Returns(false);

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.NotScheduled, outcome);
        await sutProvider.GetDependency<IBusinessPlanRenewalNotificationService>()
            .DidNotReceiveWithAnyArgs().SendRenewalEmailAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenScheduled_ReloadsCohortAssignmentAndCompletes(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment staleAssignment,
        OrganizationPlanMigrationCohortAssignment reloadedAssignment,
        OrganizationPlanMigrationCohort cohort)
    {
        staleAssignment.MigratedDate = null;
        staleAssignment.ScheduledDate = null;                // triggers scheduling
        reloadedAssignment.MigratedDate = null;
        reloadedAssignment.ScheduledDate = DateTime.UtcNow;   // scheduler stamped it
        reloadedAssignment.RenewalNotificationSentDate = null;
        reloadedAssignment.CohortId = cohort.Id;

        var assignmentRepository = sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>();
        assignmentRepository.GetByOrganizationIdAsync(organization.Id)
            .Returns(staleAssignment, reloadedAssignment); // first call stale, second call reloaded
        sutProvider.GetDependency<IPriceIncreaseScheduler>()
            .ScheduleForSubscription(Arg.Any<Subscription>()).Returns(true);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IBusinessPlanRenewalNotificationService>()
            .SendRenewalEmailAsync(organization, Arg.Any<Subscription>(), cohort).Returns(true);

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.Completed, outcome);
        Assert.NotNull(reloadedAssignment.RenewalNotificationSentDate);        // stamped the reloaded copy
        await assignmentRepository.Received(1).ReplaceAsync(reloadedAssignment); // not the stale one
        await assignmentRepository.DidNotReceive().ReplaceAsync(staleAssignment);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenNotifierDeclines_ReturnsCompletedWithoutNotification_AndDoesNotStamp(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment assignment, OrganizationPlanMigrationCohort cohort)
    {
        assignment.MigratedDate = null;
        assignment.ScheduledDate = DateTime.UtcNow;          // already scheduled — skip scheduling
        assignment.RenewalNotificationSentDate = null;
        assignment.CohortId = cohort.Id;
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IBusinessPlanRenewalNotificationService>()
            .SendRenewalEmailAsync(organization, Arg.Any<Subscription>(), cohort).Returns(false);

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.CompletedWithoutNotification, outcome);
        Assert.Null(assignment.RenewalNotificationSentDate);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenNotifierThrows_ReturnsCompletedWithoutNotification_AndDoesNotStamp(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment assignment, OrganizationPlanMigrationCohort cohort)
    {
        assignment.MigratedDate = null;
        assignment.ScheduledDate = DateTime.UtcNow;
        assignment.RenewalNotificationSentDate = null;
        assignment.CohortId = cohort.Id;
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortRepository>()
            .GetByIdAsync(cohort.Id).Returns(cohort);
        sutProvider.GetDependency<IBusinessPlanRenewalNotificationService>()
            .SendRenewalEmailAsync(organization, Arg.Any<Subscription>(), cohort)
            .ThrowsAsync(new Exception("mailer down"));

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.CompletedWithoutNotification, outcome);
        Assert.Null(assignment.RenewalNotificationSentDate);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenAlreadyScheduledAndNotified_ReturnsCompleted_WithoutReSchedulingOrReSending(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment assignment)
    {
        assignment.MigratedDate = null;
        assignment.ScheduledDate = DateTime.UtcNow.AddDays(-1);
        assignment.RenewalNotificationSentDate = DateTime.UtcNow.AddDays(-1);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(assignment);

        var outcome = await sutProvider.Sut.ExecuteAsync(organization, new Subscription());

        Assert.Equal(BusinessPlanMigrationResult.Completed, outcome);
        await sutProvider.GetDependency<IPriceIncreaseScheduler>()
            .DidNotReceiveWithAnyArgs().ScheduleForSubscription(default!);
        await sutProvider.GetDependency<IBusinessPlanRenewalNotificationService>()
            .DidNotReceiveWithAnyArgs().SendRenewalEmailAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ExecuteAsync_WhenSchedulingPhaseThrows_PropagatesException(
        SutProvider<BusinessPlanMigrationCoordinator> sutProvider, Organization organization,
        OrganizationPlanMigrationCohortAssignment assignment)
    {
        assignment.MigratedDate = null;
        assignment.ScheduledDate = null;
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetByOrganizationIdAsync(organization.Id).Returns(assignment);
        sutProvider.GetDependency<IPriceIncreaseScheduler>()
            .ScheduleForSubscription(Arg.Any<Subscription>())
            .ThrowsAsync(new StripeException("boom"));

        await Assert.ThrowsAsync<StripeException>(() => sutProvider.Sut.ExecuteAsync(organization, new Subscription()));
    }
}
