using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery : IPolicyRequirementQuery
{
    private readonly IPolicyRepository _policyRepository;
    private readonly PolicyRequirementRegistry _policyRequirements = new();

    public PolicyRequirementQuery(IGlobalSettings globalSettings, IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;

        // Register Policy Requirement factory functions below
        _policyRequirements.Add(SendPolicyRequirement.Create);
        _policyRequirements.Add(up
            => SsoPolicyRequirement.Create(up, globalSettings.Sso));
    }

    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
        => _policyRequirements.Get<T>()(await GetPolicyDetails(userId));

    private Task<IEnumerable<OrganizationUserPolicyDetails>> GetPolicyDetails(Guid userId) =>
        _policyRepository.GetPolicyDetailsByUserId(userId);

    /// <summary>
    /// Helper class used to register and retrieve Policy Requirement factories by type.
    /// </summary>
    private class PolicyRequirementRegistry
    {
        private readonly Dictionary<Type, CreateRequirement<IPolicyRequirement>> _registry = new();

        public void Add<T>(CreateRequirement<T> factory) where T : IPolicyRequirement
        {
            // Explicitly convert T to an IPolicyRequirement (C# doesn't do this automatically).
            IPolicyRequirement Converted(IEnumerable<OrganizationUserPolicyDetails> up) => factory(up);
            _registry.Add(typeof(T), Converted);
        }

        public CreateRequirement<T> Get<T>() where T : IPolicyRequirement
        {
            if (!_registry.TryGetValue(typeof(T), out var factory))
            {
                throw new NotImplementedException("No Policy Requirement found for " + typeof(T));
            }

            // Explicitly convert IPolicyRequirement back to T (C# doesn't do this automatically).
            // The cast here relies on the Register method correctly associating the type and factory function.
            T Converted(IEnumerable<OrganizationUserPolicyDetails> up) => (T)factory(up);
            return Converted;
        }
    }
}

