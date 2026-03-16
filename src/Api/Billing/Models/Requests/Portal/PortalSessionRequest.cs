using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Billing.Models.Requests.Portal;

/// <summary>
/// Request model for creating a Stripe billing portal session.
/// </summary>
public class PortalSessionRequest : IValidatableObject
{
    /// <summary>
    /// The URL to redirect to after the user completes their session in the billing portal.
    /// Must be a valid HTTP(S) URL.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string? ReturnUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl))
        {
            if (!Uri.TryCreate(ReturnUrl, UriKind.Absolute, out var uri))
            {
                yield return new ValidationResult(
                    "Return URL must be a valid absolute URL.",
                    [nameof(ReturnUrl)]);
                yield break;
            }

            // Prevent open redirect vulnerabilities by restricting to HTTP(S) schemes
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                yield return new ValidationResult(
                    "Return URL must use HTTP or HTTPS scheme.",
                    [nameof(ReturnUrl)]);
            }
        }
    }
}
