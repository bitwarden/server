using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.OrganizationFeatures.Commands;

public class DeleteAccessRuleCommand : IDeleteAccessRuleCommand
{
    private readonly IAccessRuleRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;

    public DeleteAccessRuleCommand(
        IAccessRuleRepository repository,
        TimeProvider timeProvider,
        IAccessAuditEventEmitter accessAuditEventEmitter)
    {
        _repository = repository;
        _timeProvider = timeProvider;
        _accessAuditEventEmitter = accessAuditEventEmitter;
    }

    public async Task DeleteAsync(Guid organizationId, Guid id, Guid? userId)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null || existing.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        // Rule name captured from the row now, since the delete is hard and nothing is left to resolve it from after.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RuleDeleted,
            OccurredAt = _timeProvider.GetUtcNow().UtcDateTime,
            OrganizationId = existing.OrganizationId,
            ActorId = userId,
            AccessRuleId = existing.Id,
            RuleName = existing.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        // Hard delete: remove the rule and clear its collection links (they become ungoverned).
        await _repository.DeleteAsync(existing);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
