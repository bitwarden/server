using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Policy requirements for the URI Match Defaults policy.
/// </summary>
public class UriMatchDefaultsPolicyRequirement : IPolicyRequirement
{
    /// <summary>
    /// The default URI match type that should be applied to new cipher logins.
    /// </summary>
    public UriMatchType DefaultUriMatchType { get; init; }

    /// <summary>
    /// Whether users are allowed to override the default URI match type for individual URIs.
    /// </summary>
    public bool AllowUserOverride { get; init; } = true;
}

public class UriMatchDefaultsPolicyRequirementFactory : BasePolicyRequirementFactory<UriMatchDefaultsPolicyRequirement>
{
    public override PolicyType PolicyType => PolicyType.UriMatchDefaults;

    public override UriMatchDefaultsPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
    {
        // Get the most restrictive settings from all applicable policies
        var policies = policyDetails.Select(p => p.GetDataModel<UriMatchDefaultsPolicyData>()).ToList();

        if (!policies.Any())
        {
            return new UriMatchDefaultsPolicyRequirement
            {
                DefaultUriMatchType = UriMatchType.Domain, // Base Domain
                AllowUserOverride = true
            };
        }

        // Use the first policy's default match type (assuming one policy per org)
        // but require user override to be allowed by ALL policies
        return new UriMatchDefaultsPolicyRequirement
        {
            DefaultUriMatchType = policies.First().DefaultUriMatchType,
            AllowUserOverride = policies.All(p => p.AllowUserOverride)
        };
    }
}
