namespace Bit.Api.Billing.Models.Responses.Portal;

/// <summary>
/// Response model containing the Stripe billing portal session URL.
/// </summary>
public class PortalSessionResponse
{
    /// <summary>
    /// The URL to redirect the user to for accessing the Stripe billing portal.
    /// </summary>
    public required string Url { get; init; }
}
