using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    Task<T> GetAsync<T>(Guid userId) where T : IRequirement;
}
