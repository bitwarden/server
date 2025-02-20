using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Disable Send and Send Options policies.
/// </summary>
public class SendPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// Indicates whether Send is disabled for the user. If true, the user should not be able to create or edit Sends.
    /// They may still delete existing Sends.
    /// </summary>
    public bool DisableSend { get; init; }
    /// <summary>
    /// Indicates whether the user is prohibited from hiding their email from the recipient of a Send.
    /// </summary>
    public bool DisableHideEmail { get; init; }

    /// <summary>
    /// Create a new SendPolicyRequirement.
    /// </summary>
    /// <param name="policyDetails">All PolicyDetails relating to the user.</param>
    /// <remarks>
    /// This is a <see cref="RequirementFactory{T}"/> for the SendPolicyRequirement.
    /// </remarks>
    public static SendPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var filteredPolicies = policyDetails
            .ExemptRoles([OrganizationUserType.Owner, OrganizationUserType.Admin])
            .ExemptStatus([OrganizationUserStatusType.Invited, OrganizationUserStatusType.Revoked])
            .ExemptProviders()
            .ToList();

        return filteredPolicies
            .GetPolicyType(PolicyType.SendOptions)
            .Select(p => p.GetDataModel<SendOptionsPolicyData>())
            .Aggregate(
                new SendPolicyRequirement
                {
                    // Set Disable Send requirement in the initial seed
                    DisableSend = filteredPolicies.GetPolicyType(PolicyType.DisableSend).Any()
                },
                (result, data) => new SendPolicyRequirement
                {
                    DisableSend = result.DisableSend,
                    DisableHideEmail = result.DisableHideEmail || data.DisableHideEmail
                });
    }
}
