namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PreAccess;

public interface IPreAccessEnforcerQuery
{
    /// <summary>
    /// Loads the policy state of the target organization and returns an <see cref="IPreAccessPolicyEnforcer"/>
    /// that can evaluate its policies against users who are about to be granted access to it.
    /// </summary>
    /// <param name="organizationId">The organization the users are joining.</param>
    /// <exception cref="PreAccessOrganizationNotFoundException">The organization does not exist.</exception>
    Task<IPreAccessPolicyEnforcer> RunAsync(Guid organizationId);
}
