using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Services.Pam.Rotation.Commands.Interfaces;

namespace Bit.Services.Pam.Rotation.Commands;

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
        PamTargetSystemMethod method,
        PamTargetSystemKind? kind,
        PamPasswordPolicy? passwordPolicy,
        bool? supportsSessionTermination)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        string? passwordPolicyJson;
        if (method == PamTargetSystemMethod.Automatic)
        {
            if (kind is null || passwordPolicy is null || supportsSessionTermination is null)
            {
                throw new BadRequestException(
                    "Kind, password policy, and session-termination capability are required for an automatic target system.");
            }

            passwordPolicyJson = PamPasswordPolicy.Serialize(passwordPolicy);
        }
        else
        {
            if (kind is not null || passwordPolicy is not null || supportsSessionTermination is not null)
            {
                throw new BadRequestException(
                    "Kind, password policy, and session-termination capability must not be set for a manual target system.");
            }

            kind = null;
            passwordPolicyJson = null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var target = new PamTargetSystem
        {
            OrganizationId = organizationId,
            Name = name,
            Method = method,
            Kind = kind,
            PasswordPolicy = passwordPolicyJson,
            SupportsSessionTermination = method == PamTargetSystemMethod.Automatic ? supportsSessionTermination : null,
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        };

        // audit (before/after): the target has no id until it is created, so the outcome carries it.
        var audit = new AccessAuditEventData
        {
            Kind = AccessAuditEventKind.TargetSystemRegistered,
            OccurredAt = now,
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
