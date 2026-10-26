using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.AccessConnector.Commands.Interfaces;
using Bit.Services.Pam.Services;

namespace Bit.Services.Pam.AccessConnector.Commands;

/// <inheritdoc cref="IRegisterTargetSystemCommand" />
public class RegisterTargetSystemCommand : IRegisterTargetSystemCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly TimeProvider _timeProvider;

    public RegisterTargetSystemCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _timeProvider = timeProvider;
    }

    public async Task<PamTargetSystem> RegisterAsync(
        Guid organizationId,
        Guid actingUserId,
        string name,
        PamTargetSystemMethod method)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var target = new PamTargetSystem
        {
            OrganizationId = organizationId,
            Name = name,
            Method = method,
            SealedData = null,
            WrappedKey = null,
            Status = PamTargetSystemStatus.PendingSeal,
            CreationDate = now,
            RevisionDate = now,
        };

        // Attempt now, outcome once the target has an id.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.TargetSystemRegistered,
            OccurredDate = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            TargetSystemName = name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        var created = await _targetSystemRepository.CreateAsync(target);

        await _accessAuditEventEmitter.EmitAsync(
            audit with { Phase = AccessAuditEventPhase.Outcome, TargetSystemId = created.Id });

        return created;
    }
}
