using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery : IPolicyRequirementQuery
{
    private readonly IPolicyRepository _policyRepository;

    /// <summary>
    /// A dictionary associating a Policy Requirement's type with its factory function.
    /// </summary>
    private readonly IReadOnlyDictionary<Type, CreateRequirement<IPolicyRequirement>> _policyRequirements;

    public PolicyRequirementQuery(IGlobalSettings globalSettings, IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
        var policyRequirements = new Dictionary<Type, CreateRequirement<IPolicyRequirement>>();

        // Register Policy Requirement factory functions below
        policyRequirements.AddRequirement(SendPolicyRequirement.Create);
        policyRequirements.AddRequirement(up
            => SsoPolicyRequirement.Create(up, globalSettings.Sso));

        _policyRequirements = policyRequirements.AsReadOnly();
    }

    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
        => _policyRequirements.GetRequirement<T>()(await GetPolicyDetails(userId));

    private Task<IEnumerable<OrganizationUserPolicyDetails>> GetPolicyDetails(Guid userId) =>
        _policyRepository.GetPolicyDetailsByUserId(userId);
}

/// <summary>
/// Extension methods used to add and retrieve IPolicyRequirements in the dictionary by their specific type.
/// </summary>
internal static class PolicyRequirementDictionaryExtensions
{
    public static void AddRequirement<T>(
        this IDictionary<Type, CreateRequirement<IPolicyRequirement>> registry,
        CreateRequirement<T> factory) where T : IPolicyRequirement
    {
        // Explicitly convert T to an IPolicyRequirement (C# doesn't do this automatically).
        IPolicyRequirement Converted(IEnumerable<OrganizationUserPolicyDetails> up) => factory(up);
        registry.Add(typeof(T), Converted);
    }

    public static CreateRequirement<T> GetRequirement<T>(
        this IReadOnlyDictionary<Type,
        CreateRequirement<IPolicyRequirement>> registry) where T : IPolicyRequirement
    {
        if (!registry.TryGetValue(typeof(T), out var factory))
        {
            throw new NotImplementedException("No Policy Requirement found for " + typeof(T));
        }

        // Explicitly convert IPolicyRequirement back to T (C# doesn't do this automatically).
        // The cast here relies on the Register method correctly associating the type and factory function.
        T Converted(IEnumerable<OrganizationUserPolicyDetails> up) => (T)factory(up);
        return Converted;
    }
}
