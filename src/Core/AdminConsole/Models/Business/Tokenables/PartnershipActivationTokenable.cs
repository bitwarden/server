using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Bit.Core.Tokens;

namespace Bit.Core.AdminConsole.Models.Business.Tokenables;

/// <summary>
/// Carries a partner activation link from the partner to the customer, who uses it to bind a partnership entitlement to
/// their own account. Holds no account identity, email, or external identifier.
/// </summary>
public class PartnershipActivationTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "BwPartnershipActivation_";
    public const string DataProtectorPurpose = "PartnershipActivationDataProtector";
    public const string TokenIdentifier = "PartnershipActivation";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    // [JsonInclude] lets System.Text.Json round-trip the internal setters, so only Core can mint a token.
    [JsonInclude]
    public string Identifier { get; internal set; } = TokenIdentifier;
    [JsonInclude]
    public Guid OrganizationPartnershipId { get; internal set; }
    [JsonInclude]
    public Guid OrganizationPartnershipEntitlementId { get; internal set; }
    /// <summary>
    /// Random value that makes each minted token distinct, for single-use enforcement at consumption.
    /// </summary>
    [JsonInclude]
    public string Nonce { get; internal set; } = null!;
    /// <summary>
    /// A validated return URL on one of the partnership's registered origins, or null.
    /// </summary>
    [JsonInclude]
    public string? ReturnUrl { get; internal set; }

    [JsonConstructor]
    public PartnershipActivationTokenable()
    {
    }

    internal PartnershipActivationTokenable(Guid organizationPartnershipId, Guid organizationPartnershipEntitlementId,
        string? returnUrl, DateTime now)
    {
        OrganizationPartnershipId = organizationPartnershipId;
        OrganizationPartnershipEntitlementId = organizationPartnershipEntitlementId;
        Nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        ReturnUrl = returnUrl;
        ExpirationDate = now + Lifetime;
    }

    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier &&
        OrganizationPartnershipId != default &&
        OrganizationPartnershipEntitlementId != default &&
        !string.IsNullOrEmpty(Nonce);
}
