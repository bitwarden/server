using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IUpdateTargetSystemPolicyCommand" />
public class UpdateTargetSystemPolicyCommand : IUpdateTargetSystemPolicyCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public UpdateTargetSystemPolicyCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IPamRotationConfigRepository configRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _configRepository = configRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task UpdateAsync(
        Guid organizationId, Guid actingUserId, Guid targetSystemId, PamPasswordPolicy passwordPolicy,
        bool supportsSessionTermination)
    {
        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (target.Method != PamTargetSystemMethod.Automatic)
        {
            throw new BadRequestException("Only automatic target systems have a password policy.");
        }

        var isWithdrawingTermination = target.SupportsSessionTermination == true && !supportsSessionTermination;
        if (isWithdrawingTermination &&
            await _configRepository.AnyByTargetSystemWithTerminateSessionsAsync(targetSystemId))
        {
            throw new BadRequestException(
                "This target system cannot withdraw session-termination support while a rotation config requires it.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.TargetSystemPolicyUpdated,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        target.PasswordPolicy = PamPasswordPolicy.Serialize(passwordPolicy);
        target.SupportsSessionTermination = supportsSessionTermination;
        target.RevisionDate = now;
        await _targetSystemRepository.ReplaceAsync(target);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
