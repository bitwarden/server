namespace Bit.Services.Pam.Services;

/// <summary>
/// Placeholder implementation of <see cref="IApproverInboxNotifier"/> that pushes nothing.
///
/// The signal it would send (<c>RefreshApproverInbox</c>) is not a push type this slice defines, and the clients do
/// not subscribe to it yet, so approvers pick up inbox changes on their next fetch. The commands still notify through
/// this seam, so landing the push type is a matter of swapping the registration.
/// </summary>
public class NoopApproverInboxNotifier : IApproverInboxNotifier
{
    public Task NotifyCollectionApproversAsync(Guid collectionId) => Task.CompletedTask;
}
