using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class CountNewSmSeatsRequiredQueryTests
{
    [Theory]
    [BitAutoData(2, 5, 2, 0)]
    [BitAutoData(0, 5, 2, 0)]
    [BitAutoData(6, 5, 2, 3)]
    [BitAutoData(2, 5, 10, 7)]
    public async Task CountNewSmSeatsRequiredAsync_ReturnsCorrectCount(
        int usersToAdd,
        int organizationSmSeats,
        int currentOccupiedSmSeats,
        int expectedNewSmSeatsRequired,
        Organization organization,
        SutProvider<CountNewSmSeatsRequiredQuery> sutProvider)
    {
        organization.UseSecretsManager = true;
        organization.SmSeats = organizationSmSeats;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(currentOccupiedSmSeats);

        var result = await sutProvider.Sut.CountNewSmSeatsRequiredAsync(organization.Id, usersToAdd);

        Assert.Equal(expectedNewSmSeatsRequired, result);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(5)]
    public async Task CountNewSmSeatsRequiredAsync_WithNullSmSeats_ReturnsZero(
        int usersToAdd,
        Organization organization,
        SutProvider<CountNewSmSeatsRequiredQuery> sutProvider)
    {
        const int expected = 0;

        organization.UseSecretsManager = true;
        organization.SmSeats = null;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.CountNewSmSeatsRequiredAsync(organization.Id, usersToAdd);

        Assert.Equal(expected, result);
    }

    [Theory, BitAutoData]
    public async Task CountNewSmSeatsRequiredAsync_WithNonExistentOrganizationId_ThrowsNotFound(
        Guid organizationId, int usersToAdd,
        SutProvider<CountNewSmSeatsRequiredQuery> sutProvider)
    {
        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.CountNewSmSeatsRequiredAsync(organizationId, usersToAdd));
    }

    [Theory, BitAutoData]
    public async Task CountNewSmSeatsRequiredAsync_WithOrganizationUseSecretsManagerFalse_ThrowsNotFound(
        Organization organization, int usersToAdd,
        SutProvider<CountNewSmSeatsRequiredQuery> sutProvider)
    {
        organization.UseSecretsManager = false;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.CountNewSmSeatsRequiredAsync(organization.Id, usersToAdd));
        Assert.Contains("Organization does not use Secrets Manager", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task CountNewSmSeatsRequiredAsync_WithSecretsManagerBeta_ReturnsZero(
        int usersToAdd,
        Organization organization,
        SutProvider<CountNewSmSeatsRequiredQuery> sutProvider)
    {
        organization.UseSecretsManager = true;
        organization.SecretsManagerBeta = true;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.CountNewSmSeatsRequiredAsync(organization.Id, usersToAdd);

        Assert.Equal(0, result);

        await sutProvider.GetDependency<IOrganizationUserRepository>().DidNotReceiveWithAnyArgs()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(default);
    }
}
