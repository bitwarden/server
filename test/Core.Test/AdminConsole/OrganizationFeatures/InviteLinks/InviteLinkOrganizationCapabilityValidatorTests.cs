using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.InviteLinks;

[SutProviderCustomize]
public class InviteLinkOrganizationCapabilityValidatorTests
{
    // The memberships that consume a seat: a brand-new membership (no existing row), or a Staged row, which is
    // excluded from the occupied seat count.
    public static IEnumerable<object?[]> SeatConsumingMemberships() =>
    [
        [null],
        [new OrganizationUser { Status = OrganizationUserStatusType.Staged, Type = OrganizationUserType.User }],
    ];

    [Theory]
    [BitMemberAutoData(nameof(SeatConsumingMemberships))]
    public async Task ValidateAsync_SeatConsumingMembershipWithSeatsAvailable_ReturnsValidRequest(
        OrganizationUser? existingOrganizationUser,
        Organization organization,
        SutProvider<InviteLinkOrganizationCapabilityValidator> sutProvider)
    {
        // Arrange
        SetupSeats(organization, seats: 10, occupiedSeats: 1, sutProvider);
        var request = BuildRequest(organization, existingOrganizationUser);

        // Act
        var result = await sutProvider.Sut.ValidateAsync(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(request, result.Request);
    }

    [Theory]
    [BitMemberAutoData(nameof(SeatConsumingMemberships))]
    public async Task ValidateAsync_SeatConsumingMembershipWithNoSeatsAvailable_ReturnsOrganizationHasNoAvailableSeats(
        OrganizationUser? existingOrganizationUser,
        Organization organization,
        SutProvider<InviteLinkOrganizationCapabilityValidator> sutProvider)
    {
        // Arrange
        SetupSeats(organization, seats: 4, occupiedSeats: 4, sutProvider);
        organization.MaxAutoscaleSeats = 4;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, existingOrganizationUser));

        // Assert
        Assert.True(result.IsError);
        Assert.IsType<OrganizationHasNoAvailableSeats>(result.AsError);
    }

    // A pending invitation already occupies a seat
    [Theory, BitAutoData]
    public async Task ValidateAsync_ExistingMember_SkipsSeatCheck(
        Organization organization, OrganizationUser existingOrganizationUser,
        SutProvider<InviteLinkOrganizationCapabilityValidator> sutProvider)
    {
        // Arrange
        existingOrganizationUser.Status = OrganizationUserStatusType.Invited;
        existingOrganizationUser.Type = OrganizationUserType.User;

        // Act
        var result = await sutProvider.Sut.ValidateAsync(BuildRequest(organization, existingOrganizationUser));

        // Assert
        Assert.True(result.IsValid);
        await sutProvider.GetDependency<IOrganizationRepository>().DidNotReceiveWithAnyArgs()
            .GetOccupiedSeatCountByOrganizationIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IPricingClient>().DidNotReceiveWithAnyArgs()
            .GetPlan(Arg.Any<PlanType>());
    }

    private static InviteLinkOrganizationCapabilityValidationRequest BuildRequest(
        Organization organization, OrganizationUser? existingOrganizationUser) =>
        new()
        {
            Organization = organization,
            ExistingOrganizationUser = existingOrganizationUser,
        };

    private static void SetupSeats(Organization organization, int seats, int occupiedSeats,
        SutProvider<InviteLinkOrganizationCapabilityValidator> sutProvider)
    {
        organization.PlanType = PlanType.EnterpriseAnnually;
        organization.Seats = seats;
        organization.MaxAutoscaleSeats = null;
        sutProvider.GetDependency<IPricingClient>()
            .GetPlan(organization.PlanType)
            .Returns(new Enterprise2023Plan(isAnnual: true));
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(new OrganizationSeatCounts { Users = occupiedSeats, Sponsored = 0 });
    }
}
