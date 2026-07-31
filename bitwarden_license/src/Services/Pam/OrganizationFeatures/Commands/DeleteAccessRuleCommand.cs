using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class DeleteAccessRuleCommand : IDeleteAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public DeleteAccessRuleCommand(
        IAccessRuleRepository repository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task DeleteAsync(Guid organizationId, Guid id, Guid? deletedBy)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // audit (before/after): record the delete attempt, then the outcome around the point of no return.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleDeleted,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = deletedBy,
            AccessRuleId = id,
            RuleName = existing.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Hard delete: remove the rule and clear its collection links (they become ungoverned). The RuleDeleted audit
        // event above already carries the rule's name, so nothing needs the row to survive.
        await _repository.DeleteAsync(existing);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
