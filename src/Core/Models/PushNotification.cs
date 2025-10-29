using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Enums;

namespace Bit.Core.Models;

// New push notification payload models should not be defined in this file
// they should instead be defined in file owned by your team.

public class PushNotificationData<T>
{
    public PushNotificationData(PushType type, T payload, string? contextId)
    {
        Type = type;
        Payload = payload;
        ContextId = contextId;
    }

    public PushType Type { get; set; }
    public T Payload { get; set; }
    public string? ContextId { get; set; }
}

public class SyncCipherPushNotification
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public IEnumerable<Guid>? CollectionIds { get; set; }
    public DateTime RevisionDate { get; set; }
}

public class SyncFolderPushNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime RevisionDate { get; set; }
}

public class UserPushNotification
{
    public Guid UserId { get; set; }
    public DateTime Date { get; set; }
}

public class SyncSendPushNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime RevisionDate { get; set; }
}

public class NotificationPushNotification
{
    public Guid Id { get; set; }
    public Priority Priority { get; set; }
    public bool Global { get; set; }
    public ClientType ClientType { get; set; }
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? InstallationId { get; set; }
    public Guid? TaskId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public DateTime? ReadDate { get; set; }
    public DateTime? DeletedDate { get; set; }
}

public class AuthRequestPushNotification
{
    public Guid UserId { get; set; }
    public Guid Id { get; set; }
}

public class OrganizationStatusPushNotification
{
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
}

public class OrganizationCollectionManagementPushNotification
{
    public Guid OrganizationId { get; init; }
    public bool LimitCollectionCreation { get; init; }
    public bool LimitCollectionDeletion { get; init; }
    public bool LimitItemDeletion { get; init; }
}

public class OrganizationBankAccountVerifiedPushNotification
{
    public Guid OrganizationId { get; set; }
}

public class ProviderBankAccountVerifiedPushNotification
{
    public Guid ProviderId { get; set; }
    public Guid AdminId { get; set; }
}

public class LogOutPushNotification
{
    public Guid UserId { get; set; }
    public PushNotificationLogOutReason? Reason { get; set; }
}
