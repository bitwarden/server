#nullable enable

using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// Represents the business requirements of how one or more enterprise policies will be enforced against a user.
/// The implementation of this interface will depend on how the policies are enforced in the relevant domain.
/// </summary>
public interface IPolicyRequirement;

/// <summary>
/// A factory function that takes a sequence of <see cref="PolicyDetails"/> and transforms them into a single
/// <see cref="IPolicyRequirement"/> for consumption by the relevant domain. This will receive *all* policy types
/// that may be enforced against a user; when implementing this delegate, you must filter out irrelevant policy types
/// as well as policies that should not be enforced against a user (e.g. due to the user's role or status).
/// </summary>
/// <remarks>
/// See <see cref="PolicyRequirementHelpers"/> for helpful extension methods.
/// </remarks>
public delegate T CreateRequirement<out T>(IEnumerable<PolicyDetails> policyDetails)
    where T : IPolicyRequirement;
