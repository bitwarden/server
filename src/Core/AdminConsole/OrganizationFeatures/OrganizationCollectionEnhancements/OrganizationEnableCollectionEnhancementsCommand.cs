using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements;

public class OrganizationEnableCollectionEnhancementsCommand : IOrganizationEnableCollectionEnhancementsCommand
{
    private readonly ICollectionRepository _collectionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrganizationEnableCollectionEnhancementsCommand> _logger;

    public OrganizationEnableCollectionEnhancementsCommand(ICollectionRepository collectionRepository,
        IGroupRepository groupRepository,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService,
        ILogger<OrganizationEnableCollectionEnhancementsCommand> logger)
    {
        _collectionRepository = collectionRepository;
        _groupRepository = groupRepository;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
        _logger = logger;
    }

    public async Task EnableCollectionEnhancements(Organization organization)
    {
        if (organization.FlexibleCollections)
        {
            throw new BadRequestException("Organization has already been migrated to the new collection enhancements");
        }

        // Log the Organization data that will change when the migration is complete
        await LogPreMigrationDataAsync(organization.Id);

        // Run the data migration script
        await _organizationRepository.EnableCollectionEnhancements(organization.Id);

        organization.FlexibleCollections = true;
        await _organizationService.ReplaceAndUpdateCacheAsync(organization);
    }

    /// <summary>
    /// This method logs the data that will be migrated to the new collection enhancements so that it can be restored if needed
    /// </summary>
    /// <param name="organizationId"></param>
    private async Task LogPreMigrationDataAsync(Guid organizationId)
    {
        var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
        // Grab Group Ids that have AccessAll enabled as it will be removed in the data migration
        var groupIdsWithAccessAllEnabled = groups
            .Where(g => g.AccessAll)
            .Select(g => g.Id)
            .ToList();

        var organizationUsers = await _organizationUserRepository.GetManyByOrganizationAsync(organizationId, type: null);
        // Grab OrganizationUser Ids that have AccessAll enabled as it will be removed in the data migration
        var organizationUserIdsWithAccessAllEnabled = organizationUsers
            .Where(ou => ou.AccessAll)
            .Select(ou => ou.Id)
            .ToList();
        // Grab OrganizationUser Ids of Manager users as that will be downgraded to User in the data migration
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
        var collectionUsers = await _collectionRepository.GetManyByOrganizationIdWithAccessAsync(organizationId);
        // Grab CollectionUser permissions that will change in the data migration
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

        _logger.LogWarning("Flexible Collections data migration started. Backup data: {LogObject}", JsonSerializer.Serialize(logObject));
    }
}
