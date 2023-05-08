using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public class SaveOrganizationUserCommand : OrganizationUserCommand, ISaveOrganizationUserCommand
{
    private readonly IEventService _eventService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IOrganizationService _organizationService;

    public SaveOrganizationUserCommand(
        ICurrentContext currentContext,
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IOrganizationService organizationService)
        : base(currentContext, organizationRepository)
    {
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _organizationService = organizationService;
    }

    public async Task SaveUserAsync(OrganizationUser user, Guid? savingUserId,
        IEnumerable<CollectionAccessSelection> collections,
        IEnumerable<Guid> groups)
    {
        if (user.Id.Equals(default(Guid)))
        {
            throw new BadRequestException("Invite the user first.");
        }

        var originalUser = await _organizationUserRepository.GetByIdAsync(user.Id);
        if (user.Equals(originalUser))
        {
            throw new BadRequestException("Please make changes before saving.");
        }

        if (savingUserId.HasValue)
        {
            await ValidateOrganizationUserUpdatePermissions(user.OrganizationId, user.Type, originalUser.Type, user.GetPermissions());
        }

        await ValidateOrganizationCustomPermissionsEnabledAsync(user.OrganizationId, user.Type);

        await ValidateCustomPermissions(user);

        if (user.Type != OrganizationUserType.Owner && !await _organizationService.HasConfirmedOwnersExceptAsync(user.OrganizationId, new[] { user.Id }))
        {
            throw new BadRequestException("Organization must have at least one confirmed owner.");
        }

        if (user.AccessAll)
        {
            // We don't need any collections if we're flagged to have all access.
            collections = new List<CollectionAccessSelection>();
        }
        await _organizationUserRepository.ReplaceAsync(user, collections);

        if (groups != null)
        {
            await _organizationUserRepository.UpdateGroupsAsync(user.Id, groups);
        }

        await _eventService.LogOrganizationUserEventAsync(user, EventType.OrganizationUser_Updated);
    }

    private async Task ValidateCustomPermissions(OrganizationUser user)
    {
        if (await _currentContext.OrganizationOwner(user.OrganizationId))
        {
            return;
        }

        if (await _currentContext.OrganizationAdmin(user.OrganizationId))
        {
            return;
        }

        var permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(user.Permissions);

        var permissionChecks = new Dictionary<string, Func<Guid, Task<bool>>>
        {
            { "ManageUsers", _currentContext.ManageUsers },
            { "AccessReports", _currentContext.AccessReports },
            { "ManageGroups", _currentContext.ManageGroups },
            { "ManagePolicies", _currentContext.ManagePolicies },
            { "ManageScim", _currentContext.ManageScim },
            { "ManageSso", _currentContext.ManageSso },
            { "AccessEventLogs", _currentContext.AccessEventLogs },
            { "AccessImportExport", _currentContext.AccessImportExport },
            { "CreateNewCollections", _currentContext.CreateNewCollections },
            { "DeleteAnyCollection", _currentContext.DeleteAnyCollection },
            { "DeleteAssignedCollections", _currentContext.DeleteAssignedCollections },
            { "EditAnyCollection", _currentContext.EditAnyCollection },
            { "EditAssignedCollections", _currentContext.EditAssignedCollections },
            { "ManageResetPassword", _currentContext.ManageResetPassword }
        };

        foreach (var kvp in permissionChecks)
        {
            var permissionName = kvp.Key;
            var permissionCheckFunction = kvp.Value;

            if (permissions.GetType().GetProperty(permissionName).GetValue(permissions) as bool? == true && !await permissionCheckFunction(user.OrganizationId))
            {
                throw new BadRequestException("Custom users can only grant the same custom permissions that they have.");
            }
        }
    }
}
