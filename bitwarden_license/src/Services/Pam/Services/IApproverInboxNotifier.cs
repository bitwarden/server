namespace Bit.Services.Pam.Services;

/// <summary>Pushes <c>RefreshApproverInbox</c> to every user who can Manage a collection.</summary>
public interface IApproverInboxNotifier
{
    Task NotifyCollectionApproversAsync(Guid collectionId);
}
