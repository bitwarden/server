using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public interface IPolicyRequirement;

public delegate T CreateRequirement<out T>(IEnumerable<PolicyDetails> policyDetails)
    where T : IPolicyRequirement;
