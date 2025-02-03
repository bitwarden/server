using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public interface IPolicyRequirement;

public delegate T CreateRequirement<T>(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    where T : IPolicyRequirement;
