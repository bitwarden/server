namespace Bit.Core.AdminConsole.Enums.Partnerships;

/// <summary>
/// A lifecycle transition requested on an existing partnership entitlement.
/// </summary>
public enum PartnershipEntitlementAction : byte
{
    /// <summary>The customer binds the entitlement to their Bitwarden account.</summary>
    Activate = 0,
    Suspend = 1,
    Resume = 2,
    Cancel = 3,
    /// <summary>The bound customer leaves the sponsorship from inside Bitwarden.</summary>
    UserExit = 4,
}
