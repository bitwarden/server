﻿using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;

public class SsoRequirement : IRequirement
{
    public bool RequireSso { get; init; }

    public static SsoRequirement Create(
        IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails,
        ISsoSettings ssoSettings)
        => new()
        {
            RequireSso = userPolicyDetails
                .GetPolicyType(PolicyType.RequireSso)
                .ExcludeProviders()
                // TODO: confirm minStatus - maybe confirmed?
                .ExcludeRevokedAndInvitedUsers()
                .Any(up =>
                    up.OrganizationUserType is not OrganizationUserType.Owner and not OrganizationUserType.Admin ||
                    ssoSettings.EnforceSsoPolicyForAllUsers)
        };
}
