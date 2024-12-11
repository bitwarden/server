using System.Runtime.Serialization;

namespace Bit.Core.Tools.Enums;

public enum ReferenceEventType
{
    [EnumMember(Value = "signup-email-submit")]
    SignupEmailSubmit,

    [EnumMember(Value = "signup-email-clicked")]
    SignupEmailClicked,

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
    OrganizationEditedByAdmin,

    [EnumMember(Value = "organization-created-by-admin")]
    OrganizationCreatedByAdmin,

    [EnumMember(Value = "organization-edited-in-stripe")]
    OrganizationEditedInStripe,

    [EnumMember(Value = "sm-service-account-accessed-secret")]
    SmServiceAccountAccessedSecret,
}
