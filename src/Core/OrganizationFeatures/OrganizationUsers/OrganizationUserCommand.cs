using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers;

public abstract class OrganizationUserCommand
{
    protected readonly ICurrentContext _currentContext;
    protected readonly IOrganizationRepository _organizationRepository;

    protected OrganizationUserCommand(ICurrentContext currentContext, IOrganizationRepository organizationRepository)
    {
        _currentContext = currentContext;
        _organizationRepository = organizationRepository;
    }

    protected async Task ValidateOrganizationUserUpdatePermissions(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType, Permissions permissions)
    {
        if (await _currentContext.OrganizationOwner(organizationId))
        {
            return;
        }

        if (oldType == OrganizationUserType.Owner || newType == OrganizationUserType.Owner)
        {
            throw new BadRequestException("Only an Owner can configure another Owner's account.");
        }

        if (await _currentContext.OrganizationAdmin(organizationId))
        {
            return;
        }

        if (!await _currentContext.ManageUsers(organizationId))
        {
            throw new BadRequestException("Your account does not have permission to manage users.");
        }

        if (oldType == OrganizationUserType.Admin || newType == OrganizationUserType.Admin)
        {
            throw new BadRequestException("Custom users can not manage Admins or Owners.");
        }

        if (newType == OrganizationUserType.Custom && !await ValidateCustomPermissionsGrant(organizationId, permissions))
        {
            throw new BadRequestException("Custom users can only grant the same custom permissions that they have.");
        }
    }

    protected async Task ValidateOrganizationCustomPermissionsEnabledAsync(Guid organizationId, OrganizationUserType newType)
    {
        if (newType != OrganizationUserType.Custom)
        {
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization == null)
        {
            throw new NotFoundException();
        }

        if (!organization.UseCustomPermissions)
        {
            throw new BadRequestException("To enable custom permissions the organization must be on an Enterprise plan.");
        }
    }

    private async Task<bool> ValidateCustomPermissionsGrant(Guid organizationId, Permissions permissions)
    {
        if (permissions == null || await _currentContext.OrganizationOwner(organizationId) || await _currentContext.OrganizationAdmin(organizationId))
        {
            return true;
        }

        if (permissions.ManageUsers && !await _currentContext.ManageUsers(organizationId))
        {
            return false;
        }

        if (permissions.AccessReports && !await _currentContext.AccessReports(organizationId))
        {
            return false;
        }

        if (permissions.ManageGroups && !await _currentContext.ManageGroups(organizationId))
        {
            return false;
        }

        if (permissions.ManagePolicies && !await _currentContext.ManagePolicies(organizationId))
        {
            return false;
        }

        if (permissions.ManageScim && !await _currentContext.ManageScim(organizationId))
        {
            return false;
        }

        if (permissions.ManageSso && !await _currentContext.ManageSso(organizationId))
        {
            return false;
        }

        if (permissions.AccessEventLogs && !await _currentContext.AccessEventLogs(organizationId))
        {
            return false;
        }

        if (permissions.AccessImportExport && !await _currentContext.AccessImportExport(organizationId))
        {
            return false;
        }

        if (permissions.CreateNewCollections && !await _currentContext.CreateNewCollections(organizationId))
        {
            return false;
        }

        if (permissions.DeleteAnyCollection && !await _currentContext.DeleteAnyCollection(organizationId))
        {
            return false;
        }

        if (permissions.DeleteAssignedCollections && !await _currentContext.DeleteAssignedCollections(organizationId))
        {
            return false;
        }

        if (permissions.EditAnyCollection && !await _currentContext.EditAnyCollection(organizationId))
        {
            return false;
        }

        if (permissions.EditAssignedCollections && !await _currentContext.EditAssignedCollections(organizationId))
        {
            return false;
        }

        if (permissions.ManageResetPassword && !await _currentContext.ManageResetPassword(organizationId))
        {
            return false;
        }

        return true;
    }
}
