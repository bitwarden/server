using Bit.Core.AdminConsole.Enums;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Validators;

public class CustomUserActingOnAdminValidator(ICurrentContext currentContext) : ICustomUserActingOnAdminValidator
{
    // Memoize OrganizationCustom answers for the lifetime of this request-scoped instance.
    private readonly Dictionary<Guid, bool> _organizationCustomLookup = new();

    public async Task EnforceAsync(OrganizationUser targetUser, OrganizationUserActionType actionType)
    {
        if (await IsBlockedAsync(targetUser))
        {
            throw new BadRequestException(actionType.ToCustomUserCannotModifyAdminMessage());
        }
    }

    public async Task<bool> IsBlockedAsync(OrganizationUser targetUser)
    {
        if (targetUser.Type != OrganizationUserType.Admin)
        {
            return false;
        }

        return await IsActingUserCustomAsync(targetUser.OrganizationId);
    }

    private async Task<bool> IsActingUserCustomAsync(Guid organizationId)
    {
        if (_organizationCustomLookup.TryGetValue(organizationId, out var cached))
        {
            return cached;
        }

        var isCustom = await currentContext.OrganizationCustom(organizationId);
        _organizationCustomLookup[organizationId] = isCustom;
        return isCustom;
    }
}

internal static class OrganizationUserActionExtensions
{
    public static string ToCustomUserCannotModifyAdminMessage(this OrganizationUserActionType actionType) => actionType switch
    {
        OrganizationUserActionType.Remove => "Custom users can not remove admins.",
        OrganizationUserActionType.Revoke => "Custom users can not revoke admins.",
        OrganizationUserActionType.Restore => "Custom users can not restore admins.",
        _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null),
    };
}
