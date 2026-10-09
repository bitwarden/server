namespace Bit.Core.AdminConsole.Enums.Partnerships;

/// <summary>
/// How a customer proves they hold a partner's entitlement when binding it to a Bitwarden account.
/// </summary>
public enum PartnershipBindingMode : byte
{
    /// <summary>The signed activation link is the only assertion.</summary>
    Token = 0,
    /// <summary>The customer also authenticates against the partner's identity provider.</summary>
    Oidc = 1,
}
