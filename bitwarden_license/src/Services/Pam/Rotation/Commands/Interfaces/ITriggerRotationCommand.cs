namespace Bit.Services.Pam.Rotation.Commands.Interfaces;

public interface ITriggerRotationCommand
{
    /// <summary>
    /// Triggers an on-demand rotation for a config (spec <c>TriggerRotationNow</c>). Guards: the surface guard
    /// <c>can_offer</c> (enabled, automatic target, target active, no active job) and the on-demand cooldown since
    /// <c>LastRotationAt</c>. Delegates the actual offer to <see cref="IOfferRotationCommand"/> with
    /// <see cref="Bit.Pam.Enums.PamRotationSource.OnDemand"/> — the audit trail is written there.
    /// </summary>
    Task TriggerAsync(Guid organizationId, Guid actingUserId, Guid configId);
}
