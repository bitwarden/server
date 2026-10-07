namespace Bit.Services.Pam.Services;

/// <summary>
/// Pushes <c>RefreshApproverInbox</c> to every user who can Manage a collection so their clients re-fetch the
/// approver inbox. Fired on any change the inbox renders.
/// </summary>
public interface IApproverInboxNotifier
{
    Task NotifyCollectionApproversAsync(Guid collectionId);
}
