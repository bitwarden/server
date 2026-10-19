using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class CountNewPamSeatsRequiredQueryTests
{
    [Theory]
    [BitAutoData(2, 10, 7, 0)]
    [BitAutoData(3, 10, 7, 0)]
    [BitAutoData(5, 10, 7, 2)]
    [BitAutoData(2, 10, 12, 4)]
    [BitAutoData(1, 0, 0, 1)]
    public async Task CountNewPamSeatsRequiredAsync_ReturnsCorrectCount(
        int usersToAdd,
        int organizationPamSeats,
        int currentOccupiedPamSeats,
        int expectedNewPamSeatsRequired,
        Organization organization,
        SutProvider<CountNewPamSeatsRequiredQuery> sutProvider)
    {
        organization.PamSeats = organizationPamSeats;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(currentOccupiedPamSeats);

        var result = await sutProvider.Sut.CountNewPamSeatsRequiredAsync(organization, usersToAdd);

        Assert.Equal(expectedNewPamSeatsRequired, result);
    }

    [Theory, BitAutoData]
    public async Task CountNewPamSeatsRequiredAsync_WithZeroUsersToAdd_ReturnsZeroWithoutReadingOccupancy(
        Organization organization,
        SutProvider<CountNewPamSeatsRequiredQuery> sutProvider)
    {
        organization.PamSeats = 10;
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(12);

        var result = await sutProvider.Sut.CountNewPamSeatsRequiredAsync(organization, 0);

        Assert.Equal(0, result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CountNewPamSeatsRequiredAsync_WithNullPamSeats_ReturnsZeroWithoutReadingOccupancy(
        Organization organization,
        SutProvider<CountNewPamSeatsRequiredQuery> sutProvider)
    {
        organization.PamSeats = null;

        var result = await sutProvider.Sut.CountNewPamSeatsRequiredAsync(organization, 5);

        Assert.Equal(0, result);
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetOccupiedPamSeatCountByOrganizationIdAsync(default);
    }
}
