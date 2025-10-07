
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class OrganizationDataOwnershipPolicyValidator : IPolicyValidator
{
    private readonly IPolicyRepository _policyRepository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> _factories;
    private readonly IFeatureService _featureService;

    public OrganizationDataOwnershipPolicyValidator(
        IPolicyRepository policyRepository,
        ICollectionRepository collectionRepository,
        IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories,
        IFeatureService featureService)
    {
        _policyRepository = policyRepository;
        _collectionRepository = collectionRepository;
        _factories = factories;
        _featureService = featureService;
    }

    public PolicyType Type => PolicyType.OrganizationDataOwnership;

    public IEnumerable<PolicyType> RequiredPolicies => [];

    public Task<string> ValidateAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.FromResult(string.Empty);
    }

    public Task OnSaveSideEffectsAsync(PolicyUpdate policyUpdate, Policy? currentPolicy)
    {
        return Task.CompletedTask;
    }

    private async Task<IEnumerable<T>> GetUserPolicyRequirementsByOrganizationIdAsync<T>(Guid organizationId, PolicyType policyType) where T : IPolicyRequirement
    {
        var factory = _factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var policyDetails = await _policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, policyType);
        var policyDetailGroups = policyDetails.GroupBy(policyDetail => policyDetail.UserId);
        var requirements = new List<T>();

        foreach (var policyDetailGroup in policyDetailGroups)
        {
            var filteredPolicies = policyDetailGroup
                .Where(factory.Enforce)
                .ToList();

            requirements.Add(factory.Create(filteredPolicies));
        }

        return requirements;
    }
}
