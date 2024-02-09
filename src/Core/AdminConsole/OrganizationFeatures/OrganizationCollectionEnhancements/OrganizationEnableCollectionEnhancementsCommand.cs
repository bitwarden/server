using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

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
        if (organization.FlexibleCollections)
        {
            throw new BadRequestException("Organization has already been migrated to the new collection enhancements");
        }

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
            .Select(g => g.Id)
            .ToList();

        var organizationUsers = await organizationUserRepository.GetManyByOrganizationAsync(organizationId, type: null);
        var organizationUserIdsWithAccessAllEnabled = organizationUsers
            .Where(ou => ou.AccessAll)
            .Select(ou => ou.Id)
            .ToList();
        var migratedManagers = organizationUsers
            .Where(ou => ou.Type == OrganizationUserType.Manager)
            .Select(ou => ou.Id)
            .ToList();

        var usersEligibleToManageCollections = organizationUsers
            .Where(ou =>
                ou.Type == OrganizationUserType.Manager ||
                (ou.Type == OrganizationUserType.Custom &&
                 !string.IsNullOrEmpty(ou.Permissions) &&
                 ou.GetPermissions().EditAssignedCollections)
            )
            .Select(ou => ou.Id)
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
                }))
            .Where(cud => usersEligibleToManageCollections.Any(ou => ou == cud.OrganizationUserId))
            .ToList();

        var logObject = new
        {
            OrganizationId = organizationId,
            GroupAccessAll = groupIdsWithAccessAllEnabled,
            UserAccessAll = organizationUserIdsWithAccessAllEnabled,
            MigratedManagers = migratedManagers,
            CollectionUsers = collectionUsersData
        };

        logger.LogWarning("Flexible Collections data migration started. Backup data: {@LogObject}", logObject);
    }
}
