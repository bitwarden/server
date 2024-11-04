﻿using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Extensions;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public record SendOptionsPolicyRequirementDefinition : IPolicyRequirementDefinition<SendOptionsPolicyRequirement>
{
    public PolicyType Type => PolicyType.SendOptions;

    public SendOptionsPolicyRequirement Reduce(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails) =>
        userPolicyDetails
            .Select(up => up.GetDataModel<SendOptionsPolicyData>())
            .Aggregate(
                new SendOptionsPolicyRequirement(),
                (result, current) => new SendOptionsPolicyRequirement
                {
                    DisableHideEmail = result.DisableHideEmail || current.DisableHideEmail
                });

    public bool FilterPredicate(OrganizationUserPolicyDetails userPolicyDetails) =>
        userPolicyDetails.OrganizationUserStatus > OrganizationUserStatusType.Invited;
}

public class SendOptionsPolicyRequirement : SendOptionsPolicyData, IPolicyRequirement;

