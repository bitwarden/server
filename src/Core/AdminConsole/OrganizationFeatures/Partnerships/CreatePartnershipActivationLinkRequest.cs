using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public record CreatePartnershipActivationLinkRequest
{
    public const int ReturnUrlMaxLength = 2048;

    public required OrganizationPartnership Partnership { get; init; }
    public required string ExternalId { get; init; }
    /// <summary>
    /// Where to send the customer after activation. Must be an absolute https URL on one of the partnership's
    /// registered return origins.
    /// </summary>
    public string? ReturnUrl { get; init; }
}

/// <param name="ActivationUrl">The web vault activation URL carrying <paramref name="Token"/>.</param>
/// <param name="Token">The protected activation token.</param>
/// <param name="ExpiresAt">When the token expires, in UTC.</param>
public record PartnershipActivationLink(string ActivationUrl, string Token, DateTime ExpiresAt);
