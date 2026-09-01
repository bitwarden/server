namespace Bit.Services.Pam.Services;

/// <summary>
/// Pushes the <c>RefreshApproverInbox</c> signal to every user who can Manage a collection, telling their clients to
/// re-fetch the approver inbox. Fired whenever something the inbox renders changes (a new pending request, a request
/// leaving pending, a lease being revoked).
/// </summary>
public interface IApproverInboxNotifier
{
    Task NotifyCollectionApproversAsync(Guid collectionId);
}
