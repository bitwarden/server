using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public interface IRequirement;

public delegate T CreateRequirement<T>(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    where T : IRequirement;

public class PolicyRequirementQuery : IPolicyRequirementQuery
{
    private readonly IPolicyRepository _policyRepository;

    private readonly Dictionary<Type, Func<IEnumerable<OrganizationUserPolicyDetails>, IRequirement>> _registry = new();

    public PolicyRequirementQuery(IGlobalSettings globalSettings, IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;

        // Register Requirement factory functions below
        Register(SendRequirement.Create);
        Register(up => SsoRequirement.Create(up, globalSettings.Sso));
    }

    public async Task<T> GetAsync<T>(Guid userId) where T : IRequirement => GetRequirementFactory<T>()(await GetPolicyDetails(userId));

    private Task<IEnumerable<OrganizationUserPolicyDetails>> GetPolicyDetails(Guid userId) =>
        _policyRepository.GetPolicyDetailsByUserId(userId);

    private void Register<T>(Func<IEnumerable<OrganizationUserPolicyDetails>, T> factory) where T : IRequirement
    {
        IRequirement Converted(IEnumerable<OrganizationUserPolicyDetails> up) => factory(up);
        _registry.Add(typeof(T), Converted);
    }

    private Func<IEnumerable<OrganizationUserPolicyDetails>, T> GetRequirementFactory<T>() where T : IRequirement
    {
        if (!_registry.TryGetValue(typeof(T), out var factory))
        {
            throw new NotImplementedException("No Policy Requirement found for " + typeof(T));
        }

        T Converted(IEnumerable<OrganizationUserPolicyDetails> up) => (T)factory(up);
        return Converted;
    }
}
