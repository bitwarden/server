using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Context;

public static class CurrentContextExtensions
{
    public static async Task<bool> AnyOrgUserHasManageResetPasswordPermission(this ICurrentContext currentContext,
        ICollection<OrganizationUserOrganizationDetails> organizationUserDetails)
    {
        var hasManageResetPasswordPermission = false;
        // if user has a single org with manage reset password permission, then they have it
        foreach (var orgUserDetail in organizationUserDetails)
        {
            if (await currentContext.ManageResetPassword(orgUserDetail.OrganizationId))
            {
                hasManageResetPasswordPermission = true;
                break;
            }
        }
        return hasManageResetPasswordPermission;
    }
}
