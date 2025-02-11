#nullable enable

using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery : IPolicyRequirementQuery
{
    private readonly IPolicyRepository _policyRepository;
    protected readonly PolicyRequirementRegistry PolicyRequirements = new();

    public PolicyRequirementQuery(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;

        // Register Policy Requirement factory functions below
    }

    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
        => PolicyRequirements.Get<T>()(await GetPolicyDetails(userId));

    private Task<IEnumerable<PolicyDetails>> GetPolicyDetails(Guid userId) =>
        _policyRepository.GetPolicyDetailsByUserId(userId);

    /// <summary>
    /// Helper class used to register and retrieve Policy Requirement factories by type.
    /// </summary>
    protected class PolicyRequirementRegistry
    {
        private readonly Dictionary<Type, CreateRequirement<IPolicyRequirement>> _registry = new();

        public void Add<T>(CreateRequirement<T> factory) where T : IPolicyRequirement
        {
            // Explicitly convert T to an IPolicyRequirement (C# doesn't do this automatically).
            IPolicyRequirement Converted(IEnumerable<PolicyDetails> p) => factory(p);
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
            T Converted(IEnumerable<PolicyDetails> p) => (T)factory(p);
            return Converted;
        }
    }
}

