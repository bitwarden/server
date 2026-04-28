using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Models.StaticStore;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using OrganizationSubscriptionUpdate = Bit.Core.AdminConsole.Models.Data.Organizations.OrganizationSubscriptionUpdate;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class BulkUpdateOrganizationSubscriptionsCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WhenNoSubscriptionsNeedToBeUpdated_ThenNoSyncsOccur(
        SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        OrganizationSubscriptionUpdate[] subscriptionsToUpdate = [];

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IStripePaymentService>()
            .DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WhenOrgUpdatePassedIn_ThenSyncedThroughPaymentService(
        Organization organization,
        SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;
        organization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
            [new() { Organization = organization, Plan = new Enterprise2023Plan(true) }];

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IStripePaymentService>()
            .Received(1)
            .AdjustSeatsAsync(
                Arg.Is<Organization>(x => x.Id == organization.Id),
                Arg.Is<Plan>(x => x.Type == organization.PlanType),
                organization.Seats!.Value);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpdateSuccessfulOrganizationSyncStatusAsync(
                Arg.Is<IEnumerable<Guid>>(x => x.Contains(organization.Id)),
                Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WhenOrgUpdateFails_ThenSyncDoesNotOccur(
        Organization organization,
        Exception exception,
        SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;
        organization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
            [new() { Organization = organization, Plan = new Enterprise2023Plan(true) }];

        sutProvider.GetDependency<IStripePaymentService>()
            .AdjustSeatsAsync(
                Arg.Is<Organization>(x => x.Id == organization.Id),
                Arg.Is<Plan>(x => x.Type == organization.PlanType),
                organization.Seats!.Value).ThrowsAsync(exception);

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WhenOneOrgUpdateFailsAndAnotherSucceeds_ThenSyncOccursForTheSuccessfulOrg(
            Organization successfulOrganization,
            Organization failedOrganization,
            Exception exception,
            SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        successfulOrganization.PlanType = PlanType.EnterpriseAnnually2023;
        successfulOrganization.Seats = 2;
        failedOrganization.PlanType = PlanType.EnterpriseAnnually2023;
        failedOrganization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
        [
            new() { Organization = successfulOrganization, Plan = new Enterprise2023Plan(true) },
            new() { Organization = failedOrganization, Plan = new Enterprise2023Plan(true) }
        ];

        sutProvider.GetDependency<IStripePaymentService>()
            .AdjustSeatsAsync(
                Arg.Is<Organization>(x => x.Id == failedOrganization.Id),
                Arg.Is<Plan>(x => x.Type == failedOrganization.PlanType),
                failedOrganization.Seats!.Value).ThrowsAsync(exception);

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IStripePaymentService>()
            .Received(1)
            .AdjustSeatsAsync(
                Arg.Is<Organization>(x => x.Id == successfulOrganization.Id),
                Arg.Is<Plan>(x => x.Type == successfulOrganization.PlanType),
                successfulOrganization.Seats!.Value);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpdateSuccessfulOrganizationSyncStatusAsync(
                Arg.Is<IEnumerable<Guid>>(x => x.Contains(successfulOrganization.Id)),
                Arg.Any<DateTime>());

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(
                Arg.Is<IEnumerable<Guid>>(x => x.Contains(failedOrganization.Id)),
                Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WithFeatureFlag_WhenOrgUpdatePassedIn_ThenSyncedThroughCommand(
        Organization organization,
        SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;
        organization.Seats = 2;

        var plan = new Enterprise2023Plan(true);

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
            [new() { Organization = organization, Plan = plan }];

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand)
            .Returns(true);

        BillingCommandResult<Stripe.Subscription> successResult = new Stripe.Subscription();
        sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Run(Arg.Is<Organization>(x => x.Id == organization.Id), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(successResult);

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        // Assert
        await sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Received(1)
            .Run(
                Arg.Is<Organization>(x => x.Id == organization.Id),
                Arg.Is<OrganizationSubscriptionChangeSet>(cs =>
                    cs.Changes.Count == 1 &&
                    !cs.ChargeImmediately &&
                    cs.Changes[0].AsT3.PriceId == plan.PasswordManager.StripeSeatPlanId &&
                    cs.Changes[0].AsT3.Quantity == organization.Seats!.Value));

        await sutProvider.GetDependency<IStripePaymentService>()
            .DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpdateSuccessfulOrganizationSyncStatusAsync(
                Arg.Is<IEnumerable<Guid>>(x => x.Contains(organization.Id)),
                Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WithFeatureFlag_WhenOrgUpdateFails_ThenSyncDoesNotOccur(
        Organization organization,
        SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;
        organization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
            [new() { Organization = organization, Plan = new Enterprise2023Plan(true) }];

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand)
            .Returns(true);

        BillingCommandResult<Stripe.Subscription> failureResult = new BadRequest("error");
        sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Run(Arg.Any<Organization>(), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(failureResult);

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task BulkUpdateOrganizationSubscriptionsAsync_WithFeatureFlag_WhenOneFailsAndOneSucceeds_ThenSyncOccursForSuccessfulOrg(
        Organization successfulOrganization,
        Organization failedOrganization,
        SutProvider<BulkUpdateOrganizationSubscriptionsCommand> sutProvider)
    {
        // Arrange
        successfulOrganization.PlanType = PlanType.EnterpriseAnnually2023;
        successfulOrganization.Seats = 2;
        failedOrganization.PlanType = PlanType.EnterpriseAnnually2023;
        failedOrganization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
        [
            new() { Organization = successfulOrganization, Plan = new Enterprise2023Plan(true) },
            new() { Organization = failedOrganization, Plan = new Enterprise2023Plan(true) }
        ];

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM32581_UseUpdateOrganizationSubscriptionCommand)
            .Returns(true);

        BillingCommandResult<Stripe.Subscription> successResult = new Stripe.Subscription();
        sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Run(Arg.Is<Organization>(x => x.Id == successfulOrganization.Id), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(successResult);

        BillingCommandResult<Stripe.Subscription> failureResult = new BadRequest("error");
        sutProvider.GetDependency<IUpdateOrganizationSubscriptionCommand>()
            .Run(Arg.Is<Organization>(x => x.Id == failedOrganization.Id), Arg.Any<OrganizationSubscriptionChangeSet>())
            .Returns(failureResult);

        // Act
        await sutProvider.Sut.BulkUpdateOrganizationSubscriptionsAsync(subscriptionsToUpdate);

        // Assert
        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .UpdateSuccessfulOrganizationSyncStatusAsync(
                Arg.Is<IEnumerable<Guid>>(x => x.Contains(successfulOrganization.Id)),
                Arg.Any<DateTime>());

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(
                Arg.Is<IEnumerable<Guid>>(x => x.Contains(failedOrganization.Id)),
                Arg.Any<DateTime>());
    }
}
