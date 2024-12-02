using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    /// <summary>
    /// Returns true if the feature flag for this query is enabled. Transitional use only.
    /// </summary>
    /// <returns></returns>
    public bool IsEnabled { get; }
    /// <summary>
    /// Gets the specified <see cref="IPolicyRequirement"/> for the user.
    /// </summary>
    public Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement;
}
