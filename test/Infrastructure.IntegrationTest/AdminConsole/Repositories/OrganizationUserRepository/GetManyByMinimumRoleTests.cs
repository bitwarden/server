using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetManyByMinimumRoleTests
{
    [Theory, DatabaseData]
    public async Task GetManyByMinimumRoleAsync_Admin_ReturnsOnlyConfirmedOwnersAndAdmins(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var ownerUser = await userRepository.CreateTestUserAsync();
        var adminUser = await userRepository.CreateTestUserAsync();
        var userUser = await userRepository.CreateTestUserAsync();
        var customUser = await userRepository.CreateTestUserAsync();
        var owner = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, ownerUser);
        var admin = await CreateOrganizationUserAsync(
            organizationUserRepository, organization, adminUser, OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin);
        await CreateOrganizationUserAsync(
            organizationUserRepository, organization, userUser, OrganizationUserStatusType.Confirmed, OrganizationUserType.User);
        await CreateOrganizationUserAsync(
            organizationUserRepository, organization, customUser, OrganizationUserStatusType.Confirmed, OrganizationUserType.Custom);

        // Act
        var result = await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin);

        // Assert
        Assert.Equal(
            new[] { (owner.Id, ownerUser.Email), (admin.Id, adminUser.Email) }.OrderBy(x => x.Id),
            result.Select(r => (r.Id, r.Email)).OrderBy(x => x.Id));
    }

    [Theory, DatabaseData]
    public async Task GetManyByMinimumRoleAsync_Admin_ExcludesUnconfirmedAndRevokedAdminsAndOwners(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        // Control: a Confirmed Admin proves the query returns rows, so an empty-result pass cannot hide a broken query.
        var controlUser = await userRepository.CreateTestUserAsync();
        var control = await CreateOrganizationUserAsync(
            organizationUserRepository, organization, controlUser, OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin);

        await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);
        await organizationUserRepository.CreateAcceptedTestOrganizationUserAsync(
            organization, await userRepository.CreateTestUserAsync());
        await organizationUserRepository.CreateRevokedTestOrganizationUserAsync(
            organization, await userRepository.CreateTestUserAsync());
        await CreateOrganizationUserAsync(
            organizationUserRepository, organization, await userRepository.CreateTestUserAsync(),
            OrganizationUserStatusType.Staged, OrganizationUserType.Owner);

        foreach (var status in new[]
        {
            OrganizationUserStatusType.Invited,
            OrganizationUserStatusType.Accepted,
            OrganizationUserStatusType.Staged,
            OrganizationUserStatusType.Revoked
        })
        {
            await CreateOrganizationUserAsync(
                organizationUserRepository, organization, await userRepository.CreateTestUserAsync(),
                status, OrganizationUserType.Admin);
        }

        // Act
        var result = (await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)).ToList();

        // Assert
        var returned = Assert.Single(result);
        Assert.Equal(control.Id, returned.Id);
        Assert.Equal(controlUser.Email, returned.Email);
    }

    [Theory, DatabaseData]
    public async Task GetManyByMinimumRoleAsync_Admin_ExcludesOtherOrganizations(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var ownerUser = await userRepository.CreateTestUserAsync();
        var otherOwnerUser = await userRepository.CreateTestUserAsync();
        var owner = await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, ownerUser);
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(otherOrganization, otherOwnerUser);

        // Act
        var result = (await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin)).ToList();

        // Assert
        var returned = Assert.Single(result);
        Assert.Equal(owner.Id, returned.Id);
        Assert.Equal(ownerUser.Email, returned.Email);
    }

    [Theory, DatabaseData]
    public async Task GetManyByMinimumRoleAsync_Admin_ReturnsUserEmailForLinkedAccounts(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository)
    {
        // Arrange
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var ownerUser = await userRepository.CreateTestUserAsync();
        var adminUser = await userRepository.CreateTestUserAsync();
        await organizationUserRepository.CreateConfirmedTestOrganizationUserAsync(organization, ownerUser);
        await CreateOrganizationUserAsync(
            organizationUserRepository, organization, adminUser, OrganizationUserStatusType.Confirmed, OrganizationUserType.Admin);

        // Act
        var result = await organizationUserRepository.GetManyByMinimumRoleAsync(organization.Id, OrganizationUserType.Admin);

        // Assert
        Assert.Equal(
            new[] { ownerUser.Email, adminUser.Email }.OrderBy(email => email),
            result.Select(r => r.Email).OrderBy(email => email));
    }

    private static Task<OrganizationUser> CreateOrganizationUserAsync(
        IOrganizationUserRepository organizationUserRepository,
        Organization organization,
        User user,
        OrganizationUserStatusType status,
        OrganizationUserType type)
        => organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = status == OrganizationUserStatusType.Invited ? null : user.Id,
            Email = status == OrganizationUserStatusType.Invited ? user.Email : null,
            Status = status,
            Type = type
        });
}
