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
    public MasterPasswordPolicyData? EnforcedOptions { get; }

    public MasterPasswordPolicyRequirement(IEnumerable<PolicyDetails> policyDetails)
    {
        var policies = policyDetails.ToList();
        if (policies.Count == 0)
        {
            return;
        }

        var enforcedOptions = new MasterPasswordPolicyData();
        foreach (var policy in policies)
        {
            enforcedOptions.CombineWith(policy.GetDataModel<MasterPasswordPolicyData>());
        }

        // Only assign EnforcedOptions if at least one field has a meaningful value.
        // A policy saved with no options set produces an all-null MasterPasswordPolicyData,
        // and callers rely on EnforcedOptions == null to mean "no policy enforced".
        if (enforcedOptions.MinComplexity.HasValue || enforcedOptions.MinLength.HasValue ||
            (enforcedOptions.RequireLower ?? false) || (enforcedOptions.RequireUpper ?? false) ||
            (enforcedOptions.RequireNumbers ?? false) || (enforcedOptions.RequireSpecial ?? false) ||
            (enforcedOptions.EnforceOnLogin ?? false))
        {
            EnforcedOptions = enforcedOptions;
        }
    }
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
        => new(policyDetails);
}
