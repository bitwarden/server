using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

/// <summary>
/// Saving a member with a NULL ExternalName keeps the stored value, so a server version that predates
/// the column (or a caller that doesn't set it) can't erase the SCIM userName.
/// </summary>
public class OrganizationUserExternalNameTests
{
    private const string StoredName = "Jane.Doe@tenant.onmicrosoft.com";
    private const string NewName = "Jane.Smith@tenant.onmicrosoft.com";

    [Theory, DatabaseData]
    public async Task CreateAsync_StoresExternalName(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();

        var orgUser = await CreateWithExternalNameAsync(organizationUserRepository, organization);

        Assert.Equal(StoredName, (await organizationUserRepository.GetByIdAsync(orgUser.Id))!.ExternalName);
        Assert.Equal(StoredName, (await organizationUserRepository.GetDetailsByIdAsync(orgUser.Id))!.ExternalName);
    }

    [Theory, DatabaseData]
    public async Task CreateManyAsync_StoresExternalName(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = NewInvitedMember(organization);

        await organizationUserRepository.CreateManyAsync([new CreateOrganizationUser { OrganizationUser = orgUser }]);

        Assert.Equal(StoredName, (await organizationUserRepository.GetByIdAsync(orgUser.Id))!.ExternalName);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_NullExternalName_KeepsStoredValue(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await CreateWithExternalNameAsync(organizationUserRepository, organization);
        orgUser.ExternalName = null;
        orgUser.Type = OrganizationUserType.Admin;

        await organizationUserRepository.ReplaceAsync(orgUser);

        var stored = await organizationUserRepository.GetByIdAsync(orgUser.Id);
        Assert.Equal(StoredName, stored!.ExternalName);
        // The rest of the save still applies
        Assert.Equal(OrganizationUserType.Admin, stored.Type);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_WithCollections_NullExternalName_KeepsStoredValue(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await CreateWithExternalNameAsync(organizationUserRepository, organization);
        orgUser.ExternalName = null;

        await organizationUserRepository.ReplaceAsync(orgUser, []);

        Assert.Equal(StoredName, (await organizationUserRepository.GetByIdAsync(orgUser.Id))!.ExternalName);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_NewExternalName_ReplacesStoredValue(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await CreateWithExternalNameAsync(organizationUserRepository, organization);
        orgUser.ExternalName = NewName;

        await organizationUserRepository.ReplaceAsync(orgUser);

        Assert.Equal(NewName, (await organizationUserRepository.GetByIdAsync(orgUser.Id))!.ExternalName);
    }

    [Theory, DatabaseData]
    public async Task ReplaceManyAsync_NullExternalName_KeepsStoredValue_NewExternalName_ReplacesIt(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var kept = await CreateWithExternalNameAsync(organizationUserRepository, organization);
        var renamed = await CreateWithExternalNameAsync(organizationUserRepository, organization);

        // EF can only update entities that it read from the database
        var toUpdate = await organizationUserRepository.GetManyAsync([kept.Id, renamed.Id]);
        foreach (var orgUser in toUpdate)
        {
            orgUser.ExternalName = orgUser.Id == kept.Id ? null : NewName;
            orgUser.Type = OrganizationUserType.Admin;
        }

        await organizationUserRepository.ReplaceManyAsync(toUpdate);

        var storedKept = await organizationUserRepository.GetByIdAsync(kept.Id);
        Assert.Equal(StoredName, storedKept!.ExternalName);
        Assert.Equal(OrganizationUserType.Admin, storedKept.Type);
        Assert.Equal(NewName, (await organizationUserRepository.GetByIdAsync(renamed.Id))!.ExternalName);
    }

    private static async Task<OrganizationUser> CreateWithExternalNameAsync(
        IOrganizationUserRepository organizationUserRepository,
        Organization organization)
        => await organizationUserRepository.CreateAsync(NewInvitedMember(organization));

    private static OrganizationUser NewInvitedMember(Organization organization) => new()
    {
        Id = CoreHelpers.GenerateComb(),
        OrganizationId = organization.Id,
        Email = $"{Guid.NewGuid()}@example.com",
        Status = OrganizationUserStatusType.Invited,
        Type = OrganizationUserType.User,
        ExternalName = StoredName,
    };
}
