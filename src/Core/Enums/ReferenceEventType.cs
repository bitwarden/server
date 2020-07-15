using System.Runtime.Serialization;

namespace Bit.Core.Enums
{
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
    }
}
