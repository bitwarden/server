using System.Runtime.CompilerServices;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;

public static class OrganizationUserPolicyDetailsExtensions
{
    public static bool IsAdminType(this OrganizationUserPolicyDetails userPolicyDetails)
    {
        return userPolicyDetails.OrganizationUserType is OrganizationUserType.Admin or OrganizationUserType.Owner ||
               userPolicyDetails.IsProvider;
    }
}
