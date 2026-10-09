namespace Bit.Core.AdminConsole.Enums.Partnerships;

public enum PartnershipEntitlementState : byte
{
    /// <summary>The partner has declared the customer eligible; no account is bound.</summary>
    Provisioned = 0,
    /// <summary>Bound to an account; the sponsored plan is live.</summary>
    Active = 1,
    /// <summary>Paused by the partner; the binding is kept and the plan runs through the grace period.</summary>
    Suspended = 2,
    /// <summary>Ended; the binding is held until the resume window elapses.</summary>
    Canceled = 3,
}
