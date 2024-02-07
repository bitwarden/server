using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements;

public class OrganizationEnableCollectionEnhancementsCommand(
    ICollectionRepository collectionRepository,
    IGroupRepository groupRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationService organizationService,
    ILogger<OrganizationEnableCollectionEnhancementsCommand> logger)
    : IOrganizationEnableCollectionEnhancementsCommand
{
    public async Task EnableCollectionEnhancements(Organization organization)
    {
        // Log the Organization data that will change when the migration is complete
        await LogPreMigrationDataAsync(organization.Id);

        // Run the data migration script
        await organizationRepository.EnableCollectionEnhancements(organization.Id);

        organization.FlexibleCollections = true;
        await organizationService.ReplaceAndUpdateCacheAsync(organization);
    }

    private async Task LogPreMigrationDataAsync(Guid organizationId)
    {
        var groups = await groupRepository.GetManyByOrganizationIdAsync(organizationId);
        var groupIdsWithAccessAllEnabled = groups
            .Where(g => g.AccessAll)
            .Select(g => new { GroupId = g.Id })
            .ToList();

        var organizationUsers = await organizationUserRepository.GetManyByOrganizationAsync(organizationId, type: null);
        var organizationUserIdsWithAccessAllEnabled = organizationUsers
            .Where(ou => ou.AccessAll)
            .Select(ou => new { OrganizationUserId = ou.Id })
            .ToList();
        var organizationUserIdsWithMigratedTypes = organizationUsers
            .Where(ou =>
                ou.Type == OrganizationUserType.Manager ||
                (ou.Type == OrganizationUserType.Custom &&
                 !string.IsNullOrEmpty(ou.Permissions) &&
                 JObject.Parse(ou.Permissions)
                     .Children<JProperty>()
                     .All(permission =>
                         (permission.Name is "editAssignedCollections" or "deleteAssignedCollections" && (bool)permission.Value == true) ||
                         (bool)permission.Value == false))
            )
            .Select(ou => new { OrganizationUserId = ou.Id, Type = (int)ou.Type })
            .ToList();

        var collectionUsers = await collectionRepository.GetManyByOrganizationIdWithAccessAsync(organizationId);
        var collectionUsersData = collectionUsers.SelectMany(tuple =>
            tuple.Item2.Users.Select(user =>
                new
                {
                    CollectionId = tuple.Item1.Id,
                    OrganizationUserId = user.Id,
                    user.ReadOnly,
                    user.HidePasswords
                })).ToList();

        var logObject = new
        {
            OrganizationId = organizationId,
            GroupIdsWithAccessAllEnabled = groupIdsWithAccessAllEnabled,
            OrganizationUserIdsWithAccessAllEnabled = organizationUserIdsWithAccessAllEnabled,
            OrganizationUserIdsWithMigratedTypes = organizationUserIdsWithMigratedTypes,
            CollectionUsersData = collectionUsersData
        };

        logger.LogInformation("Flexible Collections data migration started. Backup data: {@LogObject}", logObject);
    }
}
