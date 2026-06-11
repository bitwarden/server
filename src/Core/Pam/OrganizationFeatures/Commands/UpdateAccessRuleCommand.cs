using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Repositories;

namespace Bit.Core.Pam.OrganizationFeatures.Commands;

public class UpdateAccessRuleCommand : IUpdateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleValidator _validator;
    private readonly TimeProvider _timeProvider;

    public UpdateAccessRuleCommand(
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

    public async Task<AccessRuleDetails> UpdateAsync(Guid organizationId, Guid id, AccessRule update,
        IEnumerable<Guid> collectionIds)
    {
        if (string.IsNullOrWhiteSpace(update.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        var existing = await _repository.GetDetailsByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var validation = _validator.Validate(update.Conditions);
        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.Error!);
        }

        var siblings = await _repository.GetManyByOrganizationIdAsync(organizationId);
        if (siblings.Any(p => p.Id != id && string.Equals(p.Name, update.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new BadRequestException("A rule with that name already exists.");
        }

        var desiredCollectionIds = await ValidateCollectionsAsync(organizationId, id, collectionIds);

        // Persist a plain AccessRule: the AccessRuleDetails returned by GetDetailsByIdAsync carries an extra
        // CollectionIds property that the base ReplaceAsync would otherwise forward to AccessRule_Update.
        var toPersist = new AccessRule
        {
            Id = existing.Id,
            OrganizationId = existing.OrganizationId,
            Name = update.Name,
            Description = update.Description,
            Conditions = update.Conditions,
            SingleActiveLease = update.SingleActiveLease,
            CreationDate = existing.CreationDate,
            RevisionDate = _timeProvider.GetUtcNow().UtcDateTime,
        };
        await _repository.ReplaceAsync(toPersist);

        var toClear = existing.CollectionIds.Except(desiredCollectionIds).ToList();
        await _repository.SetCollectionAssociationsAsync(organizationId, id, desiredCollectionIds, toClear);

        return AccessRuleDetails.From(toPersist, desiredCollectionIds);
    }

    private async Task<List<Guid>> ValidateCollectionsAsync(Guid organizationId, Guid accessRuleId,
        IEnumerable<Guid> collectionIds)
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

        if (collections.Any(c => c.AccessRuleId.HasValue && c.AccessRuleId != accessRuleId))
        {
            throw new BadRequestException("One or more collections are already governed by another access rule.");
        }

        return distinctIds;
    }
}
