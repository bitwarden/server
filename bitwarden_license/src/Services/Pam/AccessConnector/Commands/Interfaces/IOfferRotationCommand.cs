using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface IOfferRotationCommand
{
    /// <summary>
    /// The only creation point for rotation jobs. An active job or a config that cannot be offered is returned as an
    /// outcome, not thrown.
    /// </summary>
    Task<PamRotationJobCreateOutcome> OfferAsync(Guid configId, PamRotationSource source);
}
