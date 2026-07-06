using Bit.Core.Exceptions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="IPauseRotationCommand" />
public class PauseRotationCommand : IPauseRotationCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public PauseRotationCommand(
        IPamRotationConfigRepository configRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task PauseAsync(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var config = await _configRepository.GetByIdAsync(configId);
        if (config is null || config.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (!config.Enabled)
        {
            throw new BadRequestException("This rotation config is already paused.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationPaused,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = config.CipherId,
            RotationConfigId = config.Id,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        config.Enabled = false;
        config.RevisionDate = now;
        await _configRepository.ReplaceAsync(config);

        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Outcome });
    }
}
