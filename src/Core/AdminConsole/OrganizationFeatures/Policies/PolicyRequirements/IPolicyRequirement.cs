using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

/// <summary>
/// An object that represents how a <see cref="PolicyType"/> will be enforced against a user.
/// This acts as a bridge between the <see cref="Policy"/> entity saved to the database and the domain that the policy
/// affects. You may represent the impact of the policy in any way that makes sense for the domain.
/// </summary>
public interface IPolicyRequirement;
