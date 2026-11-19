using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

public class GetManyEmailsByOrganizationIdAsyncTests
{
    [Theory, DatabaseData]
    public async Task ReturnsResolvedEmailsForOnlyRequestedMembersInOrganization(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var account = await userRepository.CreateTestUserAsync();
        var fallbackAccount = await userRepository.CreateTestUserAsync();
        var emptyEmailAccount = await userRepository.CreateTestUserAsync();
        var member = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = account.Id,
            Email = "member@example.com",
            Status = OrganizationUserStatusType.Confirmed
        });
        var fallback = await organizationUserRepository.CreateTestOrganizationUserAsync(organization, fallbackAccount);
        var emptyEmail = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            UserId = emptyEmailAccount.Id,
            Email = "",
            Status = OrganizationUserStatusType.Confirmed
        });
        var staged = await organizationUserRepository.CreateAsync(new OrganizationUser
        {
            OrganizationId = organization.Id,
            Email = "staged@example.com",
            Status = OrganizationUserStatusType.Staged
        });
        var missingEmail = await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);
        await organizationUserRepository.CreateTestOrganizationUserInviteAsync(organization);
        var foreignMember = await organizationUserRepository.CreateTestOrganizationUserAsync(otherOrganization, account);

        var emails = (await organizationUserRepository.GetManyEmailsByOrganizationIdAsync(organization.Id,
                [member.Id, fallback.Id, emptyEmail.Id, staged.Id, missingEmail.Id, foreignMember.Id, Guid.NewGuid()]))
            .ToDictionary(result => result.Id, result => result.Email);

        Assert.Equal(5, emails.Count);
        Assert.Equal("member@example.com", emails[member.Id]);
        Assert.Equal(fallbackAccount.Email, emails[fallback.Id]);
        Assert.Equal("", emails[emptyEmail.Id]);
        Assert.Equal("staged@example.com", emails[staged.Id]);
        Assert.Null(emails[missingEmail.Id]);
        Assert.Empty(await organizationUserRepository.GetManyEmailsByOrganizationIdAsync(organization.Id, []));
    }
}
