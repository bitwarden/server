using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    public Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement;
}
