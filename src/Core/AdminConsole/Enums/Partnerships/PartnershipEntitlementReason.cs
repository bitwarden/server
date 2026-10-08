namespace Bit.Core.AdminConsole.Enums.Partnerships;

/// <summary>
/// The single reason vocabulary shared by lifecycle transitions, binding failures, and status events.
/// </summary>
public enum PartnershipEntitlementReason : byte
{
    BillingLapse = 0,
    CustomerRequest = 1,
    SubscriptionEnded = 2,
    FraudHold = 3,
    PlanChange = 4,
    Administrative = 5,

    UserExit = 20,
    ResumeWindowExpired = 21,
    SubscriptionTransitioned = 22,

    NotProvisioned = 40,
    AlreadyBound = 41,
    AlreadySponsored = 42,
    ActivationTokenExpired = 43,
    ActivationTokenInvalid = 44,
    IdentityAssertionMismatch = 45,
}
