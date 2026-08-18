using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class UpdateAccessRuleCommand : IUpdateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleWriteValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;

    public UpdateAccessRuleCommand(
        IAccessRuleRepository repository,
        ICollectionRepository collectionRepository,
        IAccessRuleWriteValidator validator,
        TimeProvider timeProvider,
        IAccessAuditEventEmitter accessAuditEventEmitter)
    {
        _repository = repository;
        _collectionRepository = collectionRepository;
        _validator = validator;
        _timeProvider = timeProvider;
        _accessAuditEventEmitter = accessAuditEventEmitter;
    }

    public async Task<AccessRuleDetails> UpdateAsync(Guid organizationId, Guid id, AccessRule update,
        IEnumerable<Guid> collectionIds)
    {
        var existing = await _repository.GetDetailsByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var desiredCollectionIds = await _validator.ValidateAsync(organizationId, update, collectionIds, id);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

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
            RevisionDate = now,
            LastEditedBy = update.LastEditedBy,
        };

        // audit (before/after): RuleName is the name the rule carries after the edit, so a rename is read by comparing
        // consecutive events rather than from one. The outcome waits for the collection links, so an attempt with no
        // outcome flags an edit that may have applied to the rule but not its governed collections.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleUpdated,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = update.LastEditedBy,
            AccessRuleId = id,
            RuleName = update.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        await _repository.ReplaceAsync(toPersist);

        var toClear = existing.CollectionIds.Except(desiredCollectionIds).ToList();
        await _collectionRepository.SetAccessRuleAssociationsAsync(organizationId, id, desiredCollectionIds, toClear);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });

        return AccessRuleDetails.From(toPersist, desiredCollectionIds);
    }
}
