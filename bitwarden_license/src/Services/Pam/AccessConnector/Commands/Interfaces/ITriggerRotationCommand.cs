namespace Bit.Services.Pam.AccessConnector.Commands.Interfaces;

public interface ITriggerRotationCommand
{
    /// <summary>
    /// Triggers an on-demand rotation (spec <c>TriggerRotationNow</c>), guarded by <c>can_offer</c> and the on-demand
    /// cooldown since <c>LastRotationAt</c>. <see cref="IOfferRotationCommand"/> creates the job and writes the audit
    /// event.
    /// </summary>
    Task TriggerAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
