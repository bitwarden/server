using System.Runtime.Serialization;

namespace Bit.Core.Enums;

public enum ReferenceEventType
{
    [EnumMember(Value = "signup")]
    Signup,
    [EnumMember(Value = "upgrade-plan")]
    UpgradePlan,
    [EnumMember(Value = "adjust-storage")]
    AdjustStorage,
    [EnumMember(Value = "adjust-seats")]
    AdjustSeats,
    [EnumMember(Value = "cancel-subscription")]
    CancelSubscription,
    [EnumMember(Value = "reinstate-subscription")]
    ReinstateSubscription,
    [EnumMember(Value = "delete-account")]
    DeleteAccount,
    [EnumMember(Value = "confirm-email")]
    ConfirmEmailAddress,
    [EnumMember(Value = "invited-users")]
    InvitedUsers,
    [EnumMember(Value = "rebilled")]
    Rebilled,
    [EnumMember(Value = "send-created")]
    SendCreated,
    [EnumMember(Value = "send-accessed")]
    SendAccessed,
    [EnumMember(Value = "directory-synced")]
    DirectorySynced,
    [EnumMember(Value = "vault-imported")]
    VaultImported,
    [EnumMember(Value = "cipher-created")]
    CipherCreated,
    [EnumMember(Value = "group-created")]
    GroupCreated,
    [EnumMember(Value = "collection-created")]
    CollectionCreated,
    [EnumMember(Value = "organization-edited-by-admin")]
    OrganizationEditedByAdmin
}
