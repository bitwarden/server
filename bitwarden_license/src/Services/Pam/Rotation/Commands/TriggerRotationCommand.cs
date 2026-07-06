using Bit.Core.Exceptions;
using Bit.Pam;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Rotation.Commands.Interfaces;
using Microsoft.Extensions.Options;

namespace Bit.Services.Pam.Rotation.Commands;

/// <inheritdoc cref="ITriggerRotationCommand" />
public class TriggerRotationCommand : ITriggerRotationCommand
{
    private readonly IPamRotationConfigRepository _configRepository;
    private readonly IPamTargetSystemRepository _targetSystemRepository;
    private readonly IOfferRotationCommand _offerRotationCommand;
    private readonly IOptions<PamRotationOptions> _options;
    private readonly TimeProvider _timeProvider;

    public TriggerRotationCommand(
        IPamRotationConfigRepository configRepository,
        IPamTargetSystemRepository targetSystemRepository,
        IOfferRotationCommand offerRotationCommand,
        IOptions<PamRotationOptions> options,
        TimeProvider timeProvider)
    {
        _configRepository = configRepository;
        _targetSystemRepository = targetSystemRepository;
        _offerRotationCommand = offerRotationCommand;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task TriggerAsync(Guid organizationId, Guid actingUserId, Guid configId)
    {
        var details = await _configRepository.GetDetailsByIdAsync(configId);
        if (details is null || details.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        var target = await _targetSystemRepository.GetByIdAsync(details.TargetSystemId);
        if (target is null)
        {
            throw new NotFoundException();
        }

        // Surface guard can_offer: enabled, automatic, target active, and no active job (the last of which is not
        // part of the pure PamRotationRules.CanOffer predicate, since it needs a repository lookup).
        var canOffer = PamRotationRules.CanOffer(details, details.TargetSystemMethod, target.Status)
            && !details.HasActiveJob;
        if (!canOffer)
        {
            throw new BadRequestException("This rotation config cannot be triggered right now.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (details.LastRotationAt is { } lastRotationAt && now - lastRotationAt < _options.Value.OnDemandCooldown)
        {
            throw new BadRequestException("This rotation config was rotated recently; try again later.");
        }

        // No audit here -- OfferRotationCommand is the single creation point and writes the `offered` audit event
        // itself (RotationSource.OnDemand distinguishes this from a scheduled or access-end offer).
        await _offerRotationCommand.OfferAsync(configId, PamRotationSource.OnDemand);
    }
}
