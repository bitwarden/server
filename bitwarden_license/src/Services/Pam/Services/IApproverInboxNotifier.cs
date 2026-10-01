namespace Bit.Services.Pam.Services;

/// <summary>
/// Pushes the <c>RefreshApproverInbox</c> signal to every user who can Manage a collection, telling their clients
/// to re-fetch the approver inbox. Fired on any change the inbox renders.
/// </summary>
public interface IApproverInboxNotifier
{
    Task NotifyCollectionApproversAsync(Guid collectionId);
}
