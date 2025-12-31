using System.Security.Claims;
using Bit.Core.Billing.Licenses.Extensions;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Models.Api;

namespace Bit.Core.Billing.Licenses.Models.Api.Response;

/// <summary>
/// Response model containing user license information.
/// Separated from subscription data to maintain separation of concerns.
/// </summary>
public class LicenseResponseModel : ResponseModel
{
    public LicenseResponseModel(UserLicense license, ClaimsPrincipal? claimsPrincipal)
        : base("license")
    {
        License = license;

        // CRITICAL: When a license has a Token (JWT), ALWAYS use the expiration from the token claim
        // The token's expiration is cryptographically secured and cannot be tampered with
        // The file's Expires property can be manually edited and should NOT be trusted for display
        if (claimsPrincipal != null)
        {
            Expiration = claimsPrincipal.GetValue<DateTime?>(UserLicenseConstants.Expires);
        }
        else
        {
            // No token - use the license file expiration (for older licenses without tokens)
            Expiration = license.Expires;
        }
    }

    /// <summary>
    /// The user's license containing feature entitlements and metadata.
    /// </summary>
    public UserLicense License { get; set; }

    /// <summary>
    /// The license expiration date.
    /// Extracted from the cryptographically secured JWT token when available,
    /// otherwise falls back to the license file's expiration date.
    /// </summary>
    public DateTime? Expiration { get; set; }
}
