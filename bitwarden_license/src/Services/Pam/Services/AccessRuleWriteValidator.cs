using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.Services;

/// <summary>Validation shared by the access rule create and update paths.</summary>
public class AccessRuleWriteValidator : IAccessRuleWriteValidator
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleValidator _conditionsValidator;

    public AccessRuleWriteValidator(
        IAccessRuleRepository repository,
        ICollectionRepository collectionRepository,
        IAccessRuleValidator conditionsValidator)
    {
        _repository = repository;
        _collectionRepository = collectionRepository;
        _conditionsValidator = conditionsValidator;
    }

    public async Task<List<Guid>> ValidateAsync(Guid organizationId, AccessRule rule,
        IEnumerable<Guid> collectionIds, Guid? existingRuleId = null)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        if (rule.AllowsExtensions && rule.MaxExtensionDurationSeconds is not > 0)
        {
            throw new BadRequestException("A maximum extension length is required when extensions are allowed.");
        }

        if (rule.DefaultLeaseDurationSeconds is <= 0)
        {
            throw new BadRequestException("The default lease duration must be a positive value.");
        }

        if (rule.MaxLeaseDurationSeconds is <= 0)
        {
            throw new BadRequestException("The maximum lease duration must be a positive value.");
        }

        // A default above the rule's own cap is unsatisfiable: every request pre-filled with it would be refused
        // at submit.
        if (rule.DefaultLeaseDurationSeconds > rule.MaxLeaseDurationSeconds)
        {
            throw new BadRequestException("The default lease duration cannot exceed the maximum lease duration.");
        }

        // Refused on write rather than clamped on read, or the admin console would echo a value EffectiveMax ignores.
        // Bounds each value, not a lease's total length once extended.
        if (rule.DefaultLeaseDurationSeconds is > LeaseDurationBounds.GlobalMaxSeconds
            || rule.MaxLeaseDurationSeconds is > LeaseDurationBounds.GlobalMaxSeconds
            || rule.MaxExtensionDurationSeconds is > LeaseDurationBounds.GlobalMaxSeconds)
        {
            throw new BadRequestException(
                $"A lease duration cannot exceed {LeaseDurationBounds.GlobalMaxSeconds} seconds.");
        }

        var conditions = _conditionsValidator.Validate(rule.Conditions);
        if (!conditions.IsValid)
        {
            throw new BadRequestException(conditions.Error!);
        }

        await ValidateNameIsUniqueAsync(organizationId, rule.Name, existingRuleId);

        return await ValidateCollectionsAsync(organizationId, collectionIds, existingRuleId);
    }

    private async Task ValidateNameIsUniqueAsync(Guid organizationId, string name, Guid? existingRuleId)
    {
        var siblings = await _repository.GetManyByOrganizationIdAsync(organizationId);
        if (siblings.Any(r => r.Id != existingRuleId && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("A rule with that name already exists.");
        }
    }

    private async Task<List<Guid>> ValidateCollectionsAsync(Guid organizationId, IEnumerable<Guid> collectionIds,
        Guid? existingRuleId)
    {
        var distinctIds = collectionIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return distinctIds;
        }

        var collections = await _collectionRepository.GetManyByManyIdsAsync(distinctIds);
        if (collections.Count != distinctIds.Count)
        {
            throw new BadRequestException("One or more collections could not be found.");
        }

        if (collections.Any(c => c.OrganizationId != organizationId))
        {
            throw new BadRequestException("One or more collections do not belong to this organization.");
        }

        // The FK guarantees a set link names an existing rule, so only a link to another rule conflicts; for a create,
        // any link does.
        if (collections.Any(c => c.AccessRuleId.HasValue && c.AccessRuleId != existingRuleId))
        {
            throw new BadRequestException("One or more collections are already governed by another access rule.");
        }

        return distinctIds;
    }
}
