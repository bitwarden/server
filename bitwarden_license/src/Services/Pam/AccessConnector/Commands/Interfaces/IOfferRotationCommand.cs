using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IOfferRotationCommand
{
    /// <summary>
    /// The single creation point for rotation jobs. Internal, called by <see cref="ITriggerRotationCommand"/>,
    /// the due-schedule sweep, and the access-end handler. <see cref="PamRotationJobCreateOutcome.ActiveJobExists"/>
    /// and <see cref="PamRotationJobCreateOutcome.ConfigNotOfferable"/> are returned silently, not as errors.
    /// </summary>
    Task<PamRotationJobCreateOutcome> OfferAsync(Guid configId, PamRotationSource source);
}
