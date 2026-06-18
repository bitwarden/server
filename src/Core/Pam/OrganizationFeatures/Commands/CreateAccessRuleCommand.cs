using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Pam.Repositories;
using Bit.Pam.Services;

namespace Bit.Pam.OrganizationFeatures.Commands;

public class CreateAccessRuleCommand : ICreateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CreateAccessRuleCommand(
        IAccessRuleRepository repository,
        ICollectionRepository collectionRepository,
        IAccessRuleValidator validator,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _collectionRepository = collectionRepository;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRuleDetails> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds)
    {
        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        if (rule.AllowsExtensions && rule.MaxExtensionDurationSeconds is not > 0)
        {
            throw new BadRequestException("A maximum extension length is required when extensions are allowed.");
        }

        var validation = _validator.Validate(rule.Conditions);
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error!);
        }

        var existing = await _repository.GetManyByOrganizationIdAsync(rule.OrganizationId);
        if (existing.Any(p => string.Equals(p.Name, rule.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("A rule with that name already exists.");
        }

        var desiredCollectionIds = await ValidateCollectionsAsync(rule.OrganizationId, collectionIds);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        rule.CreationDate = now;
        rule.RevisionDate = now;

        var created = await _repository.CreateAsync(rule);

        await _repository.SetCollectionAssociationsAsync(
            created.OrganizationId, created.Id, desiredCollectionIds, []);

        return AccessRuleDetails.From(created, desiredCollectionIds);
    }

    private async Task<List<Guid>> ValidateCollectionsAsync(Guid organizationId, IEnumerable<Guid> collectionIds)
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

        if (collections.Any(IsGovernedByAnotherRule))
        {
            throw new BadRequestException("One or more collections are already governed by another access rule.");
        }

        return distinctIds;
    }

    // A new rule has no Id yet, so any existing association is a conflict.
    private static bool IsGovernedByAnotherRule(Collection collection) => collection.AccessRuleId.HasValue;
}
