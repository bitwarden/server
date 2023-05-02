using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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

    protected async Task ValidateOrganizationUserUpdatePermissions(Guid organizationId, OrganizationUserType newType, OrganizationUserType? oldType)
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

        if ((oldType == OrganizationUserType.Custom || newType == OrganizationUserType.Custom) && !await _currentContext.OrganizationCustom(organizationId))
        {
            throw new BadRequestException("Only Owners and Admins can configure Custom accounts.");
        }

        if (!await _currentContext.ManageUsers(organizationId))
        {
            throw new BadRequestException("Your account does not have permission to manage users.");
        }

        if (oldType == OrganizationUserType.Admin || newType == OrganizationUserType.Admin)
        {
            throw new BadRequestException("Custom users can not manage Admins or Owners.");
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
}
