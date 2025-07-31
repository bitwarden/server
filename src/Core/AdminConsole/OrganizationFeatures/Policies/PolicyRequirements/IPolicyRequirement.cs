namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public interface IPolicyRequirement;

/// <summary>
/// Represents a single policy requirement for a single organization.
/// </summary>
public interface ISinglePolicyRequirement : IPolicyRequirement;

/// <summary>
/// Represents the aggregated (combined) policy requirements for multiple organizations.
/// </summary>
public interface IAggregatePolicyRequirement : IPolicyRequirement;
