using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class UpdateCollectionManagementSettingsCommand : IUpdateCollectionManagementSettingsCommand
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IPushNotificationService _pushNotificationService;

    public UpdateCollectionManagementSettingsCommand(
        IOrganizationRepository organizationRepository,
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IPushNotificationService pushNotificationService)
    {
        _organizationRepository = organizationRepository;
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<Organization> UpdateCollectionManagementSettingsAsync(Guid organizationId, OrganizationCollectionManagementSettings settings)
    {
        var existingOrganization = await _organizationRepository.GetByIdAsync(organizationId);
        if (existingOrganization == null)
        {
            throw new NotFoundException();
        }

        var loggingActions = CreateCollectionManagementLoggingActions(existingOrganization, settings);

        existingOrganization.LimitCollectionCreation = settings.LimitCollectionCreation;
        existingOrganization.LimitCollectionDeletion = settings.LimitCollectionDeletion;
        existingOrganization.LimitItemDeletion = settings.LimitItemDeletion;
        existingOrganization.AllowAdminAccessToAllCollectionItems = settings.AllowAdminAccessToAllCollectionItems;
        existingOrganization.RevisionDate = DateTime.UtcNow;

        await _organizationRepository.ReplaceAsync(existingOrganization);
        await _applicationCacheService.UpsertOrganizationAbilityAsync(existingOrganization);

        if (loggingActions.Any())
        {
            await Task.WhenAll(loggingActions.Select(action => action()));
        }

        await _pushNotificationService.PushSyncOrganizationCollectionManagementSettingsAsync(existingOrganization);

        return existingOrganization;
    }

    private List<Func<Task>> CreateCollectionManagementLoggingActions(
        Organization existingOrganization, OrganizationCollectionManagementSettings settings)
    {
        var loggingActions = new List<Func<Task>>();

        if (existingOrganization.LimitCollectionCreation != settings.LimitCollectionCreation)
        {
            var eventType = settings.LimitCollectionCreation
                ? EventType.Organization_CollectionManagement_LimitCollectionCreationEnabled
                : EventType.Organization_CollectionManagement_LimitCollectionCreationDisabled;
            loggingActions.Add(() => _eventService.LogOrganizationEventAsync(existingOrganization, eventType));
        }

        if (existingOrganization.LimitCollectionDeletion != settings.LimitCollectionDeletion)
        {
            var eventType = settings.LimitCollectionDeletion
                ? EventType.Organization_CollectionManagement_LimitCollectionDeletionEnabled
                : EventType.Organization_CollectionManagement_LimitCollectionDeletionDisabled;
            loggingActions.Add(() => _eventService.LogOrganizationEventAsync(existingOrganization, eventType));
        }

        if (existingOrganization.LimitItemDeletion != settings.LimitItemDeletion)
        {
            var eventType = settings.LimitItemDeletion
                ? EventType.Organization_CollectionManagement_LimitItemDeletionEnabled
                : EventType.Organization_CollectionManagement_LimitItemDeletionDisabled;
            loggingActions.Add(() => _eventService.LogOrganizationEventAsync(existingOrganization, eventType));
        }

        if (existingOrganization.AllowAdminAccessToAllCollectionItems != settings.AllowAdminAccessToAllCollectionItems)
        {
            var eventType = settings.AllowAdminAccessToAllCollectionItems
                ? EventType.Organization_CollectionManagement_AllowAdminAccessToAllCollectionItemsEnabled
                : EventType.Organization_CollectionManagement_AllowAdminAccessToAllCollectionItemsDisabled;
            loggingActions.Add(() => _eventService.LogOrganizationEventAsync(existingOrganization, eventType));
        }

        return loggingActions;
    }
}
