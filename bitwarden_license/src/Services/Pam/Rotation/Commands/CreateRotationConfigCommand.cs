using Bit.Core.Exceptions;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="ICreateRotationConfigCommand" />
public class CreateRotationConfigCommand : ICreateRotationConfigCommand
{
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IRotationScheduleCalculator _scheduleCalculator;
    private readonly IAccessAuditEventEmitter _accessAuditEventEmitter;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public CreateRotationConfigCommand(
        IPamTargetSystemRepository targetSystemRepository,
        IPamRotationConfigRepository configRepository,
        ICipherRepository cipherRepository,
        IRotationScheduleCalculator scheduleCalculator,
        IAccessAuditEventEmitter accessAuditEventEmitter,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _targetSystemRepository = targetSystemRepository;
        _configRepository = configRepository;
        _cipherRepository = cipherRepository;
        _scheduleCalculator = scheduleCalculator;
        _accessAuditEventEmitter = accessAuditEventEmitter;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<PamRotationConfig> CreateAsync(
        Guid organizationId,
        Guid actingUserId,
        Guid cipherId,
        Guid targetSystemId,
        string accountIdentity,
        bool terminateSessions,
        string? scheduleCron,
        bool rotateOnAccessEnd)
    {
        if (string.IsNullOrWhiteSpace(accountIdentity))
        {
            throw new BadRequestException("Account identity is required.");
        }

        var target = await _targetSystemRepository.GetByIdAsync(targetSystemId);
        if (target is null || target.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (target.Status != PamTargetSystemStatus.Active)
        {
            throw new BadRequestException("The target system is not active.");
        }

        // The cipher-in-org check is the generic (unfiltered) lookup, not the user-permission-scoped one -- this is
        // an org-admin operation, not a per-user access check.
        var cipher = await _cipherRepository.GetByIdAsync(cipherId);
        if (cipher is null || cipher.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        if (await _configRepository.GetByCipherIdAsync(cipherId) is not null)
        {
            throw new BadRequestException("This cipher already has a rotation config.");
        }

        if (terminateSessions &&
            !(target.Method == PamTargetSystemMethod.Automatic && target.SupportsSessionTermination == true))
        {
            throw new BadRequestException(
                "Session termination requires an automatic target system that supports it.");
        }

        _scheduleCalculator.ValidateSchedule(scheduleCron, _options.Value.MinScheduleInterval);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var config = new PamRotationConfig
        {
            OrganizationId = organizationId,
            CipherId = cipherId,
            TargetSystemId = targetSystemId,
            AccountIdentity = accountIdentity,
            TerminateSessions = terminateSessions,
            ScheduleCron = scheduleCron,
            RotateOnAccessEnd = rotateOnAccessEnd,
            NextRotationAt = _scheduleCalculator.GetNextOccurrence(scheduleCron, now),
            Enabled = true,
            CreationDate = now,
            RevisionDate = now,
        };

        // audit (before/after): use an Attempt/Outcome pair for consistency with the other admin commands, even
        // though the spec models this as a single reaction.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.RotationConfigCreated,
            OccurredAt = now,
            OrganizationId = organizationId,
            ActorId = actingUserId,
            CipherId = cipherId,
            TargetSystemId = target.Id,
            TargetSystemName = target.Name,
        };
        await _accessAuditEventEmitter.EmitAsync(audit with { Phase = AccessAuditEventPhase.Attempt });

        var created = await _configRepository.CreateAsync(config);

        await _accessAuditEventEmitter.EmitAsync(
            audit with { Phase = AccessAuditEventPhase.Outcome, RotationConfigId = created.Id });

        return created;
    }
}
