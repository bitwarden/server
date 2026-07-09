using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class UpdateAccessRuleCommand : IUpdateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleValidator _validator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public UpdateAccessRuleCommand(
        IAccessRuleRepository repository,
        ICollectionRepository collectionRepository,
        IAccessRuleValidator validator,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _collectionRepository = collectionRepository;
        _validator = validator;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<AccessRuleDetails> UpdateAsync(Guid organizationId, Guid id, AccessRule update,
        IEnumerable<Guid> collectionIds)
    {
        if (string.IsNullOrWhiteSpace(update.Name))
        {
            throw new BadRequestException("Name is required.");
        }

        if (update.AllowsExtensions && update.MaxExtensionDurationSeconds is not > 0)
        {
            throw new BadRequestException("A maximum extension length is required when extensions are allowed.");
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
            DefaultLeaseDurationSeconds = update.DefaultLeaseDurationSeconds,
            MaxLeaseDurationSeconds = update.MaxLeaseDurationSeconds,
            Enabled = update.Enabled,
            AllowsExtensions = update.AllowsExtensions,
            MaxExtensionDurationSeconds = update.MaxExtensionDurationSeconds,
            CreationDate = existing.CreationDate,
            RevisionDate = _timeProvider.GetUtcNow().UtcDateTime,
            LastEditedBy = update.LastEditedBy,
        };
        // audit (before/after): record the update attempt, then the outcome once the rule and its collection links
        // are persisted.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleUpdated,
            OccurredAt = toPersist.RevisionDate,
            OrganizationId = organizationId,
            ActorId = toPersist.LastEditedBy,
            AccessRuleId = id,
            RuleName = toPersist.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _repository.ReplaceAsync(toPersist);

        var toClear = existing.CollectionIds.Except(desiredCollectionIds).ToList();
        await _repository.SetCollectionAssociationsAsync(organizationId, id, desiredCollectionIds, toClear);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

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

        // Deletes clear Collection.AccessRuleId and the FK forbids dangling links, so any set link points at an
        // existing rule; only a link to a different rule is a conflict.
        if (collections.Any(c => c.AccessRuleId.HasValue && c.AccessRuleId != accessRuleId))
        {
            throw new BadRequestException("One or more collections are already governed by another access rule.");
        }

        return distinctIds;
    }
}
