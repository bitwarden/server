using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations;
using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Organizations;

[SutProviderCustomize]
public class UpdateOrganizationSubscriptionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationSubscriptionAsync_WhenNoSubscriptionsNeedToBeUpdated_ThenNoSyncsOccur(
        SutProvider<UpdateOrganizationSubscriptionCommand> sutProvider)
    {
        // Arrange
        OrganizationSubscriptionUpdate[] subscriptionsToUpdate = [];

        // Act
        await sutProvider.Sut.UpdateOrganizationSubscriptionAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IPaymentService>()
            .DidNotReceive()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationSubscriptionAsync_WhenOrgUpdatePassedIn_ThenSyncedThroughPaymentService(
        Organization organization,
        SutProvider<UpdateOrganizationSubscriptionCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;
        organization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
            [new() { Organization = organization, Plan = new Enterprise2023Plan(true) }];

        // Act
        await sutProvider.Sut.UpdateOrganizationSubscriptionAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IPaymentService>()
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
    public async Task UpdateOrganizationSubscriptionAsync_WhenOrgUpdateFails_ThenSyncDoesNotOccur(
        Organization organization,
        Exception exception,
        SutProvider<UpdateOrganizationSubscriptionCommand> sutProvider)
    {
        // Arrange
        organization.PlanType = PlanType.EnterpriseAnnually2023;
        organization.Seats = 2;

        OrganizationSubscriptionUpdate[] subscriptionsToUpdate =
            [new() { Organization = organization, Plan = new Enterprise2023Plan(true) }];

        sutProvider.GetDependency<IPaymentService>()
            .AdjustSeatsAsync(
                Arg.Is<Organization>(x => x.Id == organization.Id),
                Arg.Is<Plan>(x => x.Type == organization.PlanType),
                organization.Seats!.Value).ThrowsAsync(exception);

        // Act
        await sutProvider.Sut.UpdateOrganizationSubscriptionAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .DidNotReceive()
            .UpdateSuccessfulOrganizationSyncStatusAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<DateTime>());
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateOrganizationSubscriptionAsync_WhenOneOrgUpdateFailsAndAnotherSucceeds_ThenSyncOccursForTheSuccessfulOrg(
            Organization successfulOrganization,
            Organization failedOrganization,
            Exception exception,
            SutProvider<UpdateOrganizationSubscriptionCommand> sutProvider)
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

        sutProvider.GetDependency<IPaymentService>()
            .AdjustSeatsAsync(
                Arg.Is<Organization>(x => x.Id == failedOrganization.Id),
                Arg.Is<Plan>(x => x.Type == failedOrganization.PlanType),
                failedOrganization.Seats!.Value).ThrowsAsync(exception);

        // Act
        await sutProvider.Sut.UpdateOrganizationSubscriptionAsync(subscriptionsToUpdate);

        await sutProvider.GetDependency<IPaymentService>()
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
}
