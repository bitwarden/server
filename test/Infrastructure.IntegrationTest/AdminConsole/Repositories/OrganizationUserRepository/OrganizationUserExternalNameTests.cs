using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.AdminConsole.Repositories.OrganizationUserRepository;

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
    public async Task ReplaceManyAsync_NewExternalName_ReplacesStoredValue(
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var orgUser = await CreateWithExternalNameAsync(organizationUserRepository, organization);

        // EF can only update entities that it read from the database
        var toUpdate = await organizationUserRepository.GetManyAsync([orgUser.Id]);
        foreach (var member in toUpdate)
        {
            member.ExternalName = NewName;
        }

        await organizationUserRepository.ReplaceManyAsync(toUpdate);

        Assert.Equal(NewName, (await organizationUserRepository.GetByIdAsync(orgUser.Id))!.ExternalName);
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
