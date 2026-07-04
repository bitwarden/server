using Bit.Core.Entities;
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

public class CreateAccessRuleCommand : ICreateAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly ICollectionRepository _collectionRepository;
    private readonly IAccessRuleValidator _validator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public CreateAccessRuleCommand(
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

    public async Task<AccessRuleDetails> CreateAsync(AccessRule rule, IEnumerable<Guid> collectionIds)
    {
        var desiredCollectionIds = await _validator.ValidateAsync(rule.OrganizationId, rule, collectionIds);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        rule.CreationDate = now;
        rule.RevisionDate = now;

        // audit (before/after): record the create attempt, then the outcome once the rule and its collection links
        // are persisted.
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

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome, AccessRuleId = created.Id });

        return AccessRuleDetails.From(created, desiredCollectionIds);
    }
}
