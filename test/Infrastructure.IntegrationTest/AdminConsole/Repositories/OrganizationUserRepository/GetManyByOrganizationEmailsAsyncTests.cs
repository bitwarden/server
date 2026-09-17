using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetManyByOrganizationEmailsAsyncTests
{
    private static Task<OrganizationUser> CreateUnlinkedMemberAsync(
        IOrganizationUserRepository organizationUserRepository,
        Organization organization,
        string email,
        OrganizationUserStatusType status)
        => organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = null,
            Email = email,
            Status = status,
            Type = OrganizationUserType.User
        });

    /// <summary>
    /// Status is not part of the match: any member carrying their own email is returned, and nothing else is.
    /// </summary>
    [Theory, DatabaseData]
    public async Task WithMembersLackingALinkedAccount_ReturnsThemWhateverTheirStatus(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var stagedEmail = $"staged-{Guid.NewGuid()}@example.com";
        var staged = await CreateUnlinkedMemberAsync(organizationUserRepository, organization, stagedEmail,
            OrganizationUserStatusType.Staged);

        var invitedEmail = $"invited-{Guid.NewGuid()}@example.com";
        var invited = await CreateUnlinkedMemberAsync(organizationUserRepository, organization, invitedEmail,
            OrganizationUserStatusType.Invited);

        // A member of the same organization who was not asked for.
        await CreateUnlinkedMemberAsync(organizationUserRepository, organization,
            $"other-{Guid.NewGuid()}@example.com", OrganizationUserStatusType.Staged);

        var result = await organizationUserRepository.GetManyByOrganizationEmailsAsync(
            organization.Id, [stagedEmail, invitedEmail, $"nobody-{Guid.NewGuid()}@example.com"]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, ou => ou.Id == staged.Id);
        Assert.Contains(result, ou => ou.Id == invited.Id);
    }

    /// <summary>
    /// A Confirmed member's OrganizationUser.Email is null and their address lives on the linked account, so
    /// this method cannot see them. Callers that need to know whether an address is taken want
    /// SelectKnownEmailsAsync instead.
    /// </summary>
    [Theory, DatabaseData]
    public async Task WithConfirmedMember_DoesNotMatchTheirAccountEmail(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var user = await userRepository.CreateTestUserAsync();
        await organizationUserRepository.CreateTestOrganizationUserAsync(organization, user);

        var result = await organizationUserRepository.GetManyByOrganizationEmailsAsync(organization.Id, [user.Email]);

        Assert.Empty(result);
    }

    [Theory, DatabaseData]
    public async Task WithMemberOfAnotherOrganization_DoesNotReturnThem(
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var email = $"staged-{Guid.NewGuid()}@example.com";
        await CreateUnlinkedMemberAsync(organizationUserRepository, otherOrganization, email,
            OrganizationUserStatusType.Staged);

        var result = await organizationUserRepository.GetManyByOrganizationEmailsAsync(organization.Id, [email]);

        Assert.Empty(result);
    }
}
