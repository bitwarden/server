#nullable enable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the Master Password policy.
/// Owners and Admins are exempt from this policy
/// </summary>
public class MasterPasswordPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// The combined Master Password policy options enforced against the user, or null if no policy applies.
    /// </summary>
    public MasterPasswordPolicyData? EnforcedOptions { get; init; }
}

/// <summary>
/// Factory for <see cref="MasterPasswordPolicyRequirement"/>.
/// Owners and Admins are exempt from this policy, consistent with the client-side exemption.
/// Invited and Revoked users are also exempt (inherited default from <see cref="BasePolicyRequirementFactory{T}"/>),
/// which is intentional: master password requirements are enforced at login/unlock for active members only.
/// </summary>
public class MasterPasswordPolicyRequirementFactory : BasePolicyRequirementFactory<MasterPasswordPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.MasterPassword;

    public override MasterPasswordPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        var combined = policyDetails
            .Select(p => p.GetDataModel<MasterPasswordPolicyData>())
            .Aggregate(new MasterPasswordPolicyData(), (result, data) =>
            {
                result.CombineWith(data);
                return result;
            });

        // Only set EnforcedOptions if at least one field has a meaningful value.
        // A policy saved with no options set produces an all-null MasterPasswordPolicyData,
        // and callers rely on EnforcedOptions == null to mean "no policy enforced".
        var hasAnyOption = combined.MinComplexity.HasValue || combined.MinLength.HasValue ||
            (combined.RequireLower ?? false) || (combined.RequireUpper ?? false) ||
            (combined.RequireNumbers ?? false) || (combined.RequireSpecial ?? false) ||
            (combined.EnforceOnLogin ?? false);

        return new MasterPasswordPolicyRequirement
        {
            EnforcedOptions = hasAnyOption ? combined : null
        };
    }
}
