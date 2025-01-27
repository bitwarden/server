using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    Task<SingleOrganizationRequirement> GetSingleOrganizationRequirementAsync(Guid userId);
    Task<SendRequirement> GetSendRequirementAsync(Guid userId);
}
