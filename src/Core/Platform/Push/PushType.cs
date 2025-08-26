using Bit.Core.Platform.Push;

// TODO: This namespace should change to `Bit.Core.Platform.Push`
namespace Bit.Core.Enums;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// <para>
/// When adding a new enum member you must annotate it with a <see cref="NotificationInfoAttribute"/> 
/// this is enforced with a unit test. It is preferred that you do NOT add new usings for the type referenced
/// in <see cref="NotificationInfoAttribute"/>.
/// </para>
/// <para>
/// You may and are 
/// </para>
/// </remarks>
public enum PushType : byte
{
    // When adding a new enum member you must annotate it with a NotificationInfoAttribute  this is enforced with a unit
    // test. It is preferred that you do NOT add new usings for the type referenced for the payload. You are also
    // encouraged to define the payload type in your own teams owned code.

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncCipherPushNotification))]
    SyncCipherUpdate = 0,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncCipherPushNotification))]
    SyncCipherCreate = 1,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncCipherPushNotification))]
    SyncLoginDelete = 2,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncFolderPushNotification))]
    SyncFolderDelete = 3,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.UserPushNotification))]
    SyncCiphers = 4,

    [NotificationInfo("not-specified", typeof(Models.UserPushNotification))]
    SyncVault = 5,

    [NotificationInfo("@bitwarden/team-admin-console-dev", typeof(Models.UserPushNotification))]
    SyncOrgKeys = 6,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncFolderPushNotification))]
    SyncFolderCreate = 7,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncFolderPushNotification))]
    SyncFolderUpdate = 8,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.SyncCipherPushNotification))]
    SyncCipherDelete = 9,

    [NotificationInfo("not-specified", typeof(Models.UserPushNotification))]
    SyncSettings = 10,

    [NotificationInfo("not-specified", typeof(Models.UserPushNotification))]
    LogOut = 11,

    [NotificationInfo("@bitwarden/team-tools-dev", typeof(Models.SyncSendPushNotification))]
    SyncSendCreate = 12,

    [NotificationInfo("@bitwarden/team-tools-dev", typeof(Models.SyncSendPushNotification))]
    SyncSendUpdate = 13,

    [NotificationInfo("@bitwarden/team-tools-dev", typeof(Models.SyncSendPushNotification))]
    SyncSendDelete = 14,

    [NotificationInfo("@bitwarden/team-auth-dev", typeof(Models.AuthRequestPushNotification))]
    AuthRequest = 15,

    [NotificationInfo("@bitwarden/team-auth-dev", typeof(Models.AuthRequestPushNotification))]
    AuthRequestResponse = 16,

    [NotificationInfo("not-specified", typeof(Models.UserPushNotification))]
    SyncOrganizations = 17,

    [NotificationInfo("@bitwarden/team-billing-dev", typeof(Models.OrganizationStatusPushNotification))]
    SyncOrganizationStatusChanged = 18,

    [NotificationInfo("@bitwarden/team-admin-console-dev", typeof(Models.OrganizationCollectionManagementPushNotification))]
    SyncOrganizationCollectionSettingChanged = 19,

    [NotificationInfo("not-specified", typeof(Models.NotificationPushNotification))]
    Notification = 20,

    [NotificationInfo("not-specified", typeof(Models.NotificationPushNotification))]
    NotificationStatus = 21,

    [NotificationInfo("@bitwarden/team-vault-dev", typeof(Models.UserPushNotification))]
    RefreshSecurityTasks = 22,
}
