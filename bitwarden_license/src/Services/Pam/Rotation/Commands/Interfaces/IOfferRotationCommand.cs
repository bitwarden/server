using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface IOfferRotationCommand
{
    /// <summary>
    /// The single creation point for rotation jobs (spec <c>OfferRotation</c>). Internal — no organization or user
    /// context; called by <see cref="ITriggerRotationCommand"/>, the due-schedule sweep, and the access-end handler.
    /// Re-checks <c>can_offer</c> (enabled, automatic target, target active) before the guarded insert (invariant
    /// <c>AtMostOneActiveJobPerConfig</c>). <see cref="PamRotationJobCreateOutcome.ActiveJobExists"/> and
    /// <see cref="PamRotationJobCreateOutcome.ConfigNotOfferable"/> are returned silently — callers race benignly
    /// and never treat them as errors.
    /// </summary>
    Task<PamRotationJobCreateOutcome> OfferAsync(Guid configId, PamRotationSource source);
}
