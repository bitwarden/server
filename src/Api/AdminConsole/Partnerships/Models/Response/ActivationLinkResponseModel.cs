using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.HttpExtensions;

namespace Bit.Api.AdminConsole.Partnerships.Models.Response;

/// <summary>
/// A short-lived link the partner sends its customer to activate the entitlement on their Bitwarden account.
/// </summary>
public class ActivationLinkResponseModel(PartnershipActivationLink link) : ResponseModel("activationLink")
{
    /// <summary>
    /// The URL to send the customer to. Treat it as a secret until it expires.
    /// </summary>
    public string ActivationUrl { get; } = link.ActivationUrl;

    /// <summary>
    /// When the link expires, in UTC.
    /// </summary>
    public DateTime ExpiresAt { get; } = link.ExpiresAt;
}
