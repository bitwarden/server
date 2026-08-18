using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class CreateAccessRuleCommand : ICreateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleWriteValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;

    public CreateAccessRuleCommand(
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

    public async Task<AccessRuleDetails> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds)
    {
        var desiredCollectionIds = await _validator.ValidateAsync(rule.OrganizationId, rule, collectionIds);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        rule.CreationDate = now;
        rule.RevisionDate = now;

        // audit (before/after): the actor is the editor the handler stamped on the rule. The attempt cannot name the
        // rule -- Repository.CreateAsync assigns the id, so before the write there is no rule to name -- and the
        // outcome is emitted only once the collection links are written too, so an attempt with no outcome flags a
        // half-created rule rather than reading as a clean create.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleCreated,
            OccurredAt = now,
            OrganizationId = rule.OrganizationId,
            ActorId = rule.LastEditedBy,
            RuleName = rule.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        var created = await _repository.CreateAsync(rule);

        await _collectionRepository.SetAccessRuleAssociationsAsync(
            created.OrganizationId, created.Id, desiredCollectionIds, []);

        await _accessAuditEventEmitter.EmitAsync(
            audit with { Phase = AccessAuditEventPhase.Outcome, AccessRuleId = created.Id });

        return AccessRuleDetails.From(created, desiredCollectionIds);
    }
}
