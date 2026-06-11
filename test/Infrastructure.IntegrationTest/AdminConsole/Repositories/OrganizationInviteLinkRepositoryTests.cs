using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories;

public class OrganizationInviteLinkRepositoryTests
{
    [Theory, DatabaseData]
    public async Task CreateAsync_Works(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var link = await repository.CreateTestOrganizationInviteLinkAsync(organization);

        var result = await repository.GetByIdAsync(link.Id);

        Assert.NotNull(result);
        Assert.Equal(link.Id, result.Id);
        Assert.Equal(link.Code, result.Code);
        Assert.Equal(link.OrganizationId, result.OrganizationId);
        Assert.Equal(link.AllowedDomains, result.AllowedDomains);
        Assert.Equal(link.EncryptedInviteKey, result.EncryptedInviteKey);
        Assert.Equal(link.EncryptedOrgKey, result.EncryptedOrgKey);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_DuplicateOrganizationId_Throws(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        await repository.CreateTestOrganizationInviteLinkAsync(organization, "first");

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.CreateTestOrganizationInviteLinkAsync(organization, "second"));
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_DuplicateCode_Throws(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization1 = await organizationRepository.CreateTestOrganizationAsync(identifier: "org1");
        var organization2 = await organizationRepository.CreateTestOrganizationAsync(identifier: "org2");

        var sharedCode = Guid.NewGuid();

        await repository.CreateAsync(new OrganizationInviteLink
        {
            Code = sharedCode,
            OrganizationId = organization1.Id,
            AllowedDomains = "[\"example.com\"]",
            EncryptedInviteKey = "key-1",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });

        await Assert.ThrowsAnyAsync<Exception>(() => repository.CreateAsync(new OrganizationInviteLink
        {
            Code = sharedCode,
            OrganizationId = organization2.Id,
            AllowedDomains = "[\"example.com\"]",
            EncryptedInviteKey = "key-2",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        }));
    }

    [Theory, DatabaseData]
    public async Task GetByCodeAsync_ReturnsLink(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var link = await repository.CreateTestOrganizationInviteLinkAsync(organization);

        var result = await repository.GetByCodeAsync(link.Code);

        Assert.NotNull(result);
        Assert.Equal(link.Id, result.Id);
        Assert.Equal(link.Code, result.Code);
        Assert.Equal(link.EncryptedOrgKey, result.EncryptedOrgKey);
    }

    [Theory, DatabaseData]
    public async Task GetByCodeAsync_NonExistentCode_ReturnsNull(
        IOrganizationInviteLinkRepository repository)
    {
        var result = await repository.GetByCodeAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task GetByOrganizationIdAsync_ReturnsLink(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var link = await repository.CreateTestOrganizationInviteLinkAsync(organization);

        var result = await repository.GetByOrganizationIdAsync(organization.Id);

        Assert.NotNull(result);
        Assert.Equal(link.Id, result.Id);
        Assert.Equal(link.OrganizationId, result.OrganizationId);
        Assert.Equal(link.EncryptedOrgKey, result.EncryptedOrgKey);
    }

    [Theory, DatabaseData]
    public async Task GetByOrganizationIdAsync_NonExistentOrg_ReturnsNull(
        IOrganizationInviteLinkRepository repository)
    {
        var result = await repository.GetByOrganizationIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesAllowedDomains(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var link = await repository.CreateTestOrganizationInviteLinkAsync(organization);

        link.AllowedDomains = "[\"updated.com\",\"new.org\"]";
        link.EncryptedOrgKey = "updated-encrypted-org-key";
        link.RevisionDate = DateTime.UtcNow;
        await repository.ReplaceAsync(link);

        var result = await repository.GetByIdAsync(link.Id);

        Assert.NotNull(result);
        Assert.Equal("[\"updated.com\",\"new.org\"]", result.AllowedDomains);
        Assert.Equal("updated-encrypted-org-key", result.EncryptedOrgKey);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesLink(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var link = await repository.CreateTestOrganizationInviteLinkAsync(organization);

        await repository.DeleteAsync(link);

        var result = await repository.GetByIdAsync(link.Id);
        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task RefreshAsync_ReplacesLink(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var oldLink = await repository.CreateTestOrganizationInviteLinkAsync(organization);

        var newLink = new OrganizationInviteLink
        {
            Code = Guid.NewGuid(),
            OrganizationId = organization.Id,
            AllowedDomains = oldLink.AllowedDomains,
            EncryptedInviteKey = "new-encrypted-key",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };
        newLink.SetNewId();

        await repository.RefreshAsync(oldLink, newLink);

        Assert.Null(await repository.GetByIdAsync(oldLink.Id));
        var current = await repository.GetByOrganizationIdAsync(organization.Id);
        Assert.NotNull(current);
        Assert.Equal(newLink.Id, current.Id);
        Assert.Equal(newLink.Code, current.Code);
        Assert.Equal(newLink.EncryptedInviteKey, current.EncryptedInviteKey);
    }

    [Theory, DatabaseData]
    public async Task RefreshAsync_WhenInsertViolatesUniqueCode_RollsBackDelete(
        IOrganizationInviteLinkRepository repository,
        IOrganizationRepository organizationRepository)
    {
        var org1 = await organizationRepository.CreateTestOrganizationAsync(identifier: "org1");
        var org2 = await organizationRepository.CreateTestOrganizationAsync(identifier: "org2");

        var org1Link = await repository.CreateTestOrganizationInviteLinkAsync(org1);
        var org2Link = await repository.CreateTestOrganizationInviteLinkAsync(org2);

        var conflictingLink = new OrganizationInviteLink
        {
            Code = org2Link.Code,
            OrganizationId = org1.Id,
            AllowedDomains = org1Link.AllowedDomains,
            EncryptedInviteKey = "new-encrypted-key",
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };
        conflictingLink.SetNewId();

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.RefreshAsync(org1Link, conflictingLink));

        var surviving = await repository.GetByIdAsync(org1Link.Id);
        Assert.NotNull(surviving);
        Assert.Equal(org1Link.Id, surviving.Id);
    }
}
