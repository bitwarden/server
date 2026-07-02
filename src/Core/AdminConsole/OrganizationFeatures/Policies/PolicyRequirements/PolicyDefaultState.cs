namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Whether a policy is enabled or disabled by default: it determines how the policy domain interprets the absence of a
/// <see cref="Bit.Core.AdminConsole.Entities.Policy"/> row for an organization.
/// </summary>
public enum PolicyDefaultState : byte
{
    /// <summary>
    /// The policy is off unless an organization explicitly enables it (the default for all policies). A missing row
    /// reads as disabled.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// The policy is on unless an organization explicitly disables it. A missing row reads as enabled.
    /// </summary>
    Enabled = 1,
}
