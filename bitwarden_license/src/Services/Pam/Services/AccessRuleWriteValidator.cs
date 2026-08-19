using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;

namespace Bit.Services.Pam.Services;

/// <summary>
/// The shared validation for the AccessRule create and update paths. Create and update differ only in whether the
/// rule already exists, which both the name-uniqueness and collection-conflict checks express by comparing against
/// <c>existingRuleId</c> — null for a create, so nothing is excluded from either check.
/// </summary>
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

        // A default above the rule's own cap is unsatisfiable: every request pre-filled with it would be refused at
        // submit. The edit form already couples its two pickers, so this closes the same gap for a direct API write.
        if (rule.DefaultLeaseDurationSeconds > rule.MaxLeaseDurationSeconds)
        {
            throw new BadRequestException("The default lease duration cannot exceed the maximum lease duration.");
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

        // Deletes clear Collection.AccessRuleId and the FK forbids dangling links, so any set link points at an
        // existing rule; only a link to a different rule is a conflict. A rule being created has no id, so for it
        // any link at all conflicts.
        if (collections.Any(c => c.AccessRuleId.HasValue && c.AccessRuleId != existingRuleId))
        {
            throw new BadRequestException("One or more collections are already governed by another access rule.");
        }

        return distinctIds;
    }
}
