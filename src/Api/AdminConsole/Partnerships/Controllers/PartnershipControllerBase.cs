using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

/// <summary>
/// Base class for partner-facing controllers under <see cref="PartnershipRoutes.Prefix"/>. Deriving from it requires an
/// organization API key token, applies <see cref="PartnershipAccessFilter"/> so only organizations with an active
/// partnership get through, applies <see cref="PartnershipRateLimitFilter"/> to limit requests per partnership, and
/// applies <see cref="PartnershipErrorFilter"/> so every error uses the partnership envelope.
/// </summary>
[Authorize(Policies.Organization)]
[ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status500InternalServerError)]
public abstract class PartnershipControllerBase : Controller
{
    /// <summary>
    /// Maps a domain error to its partnership status code and envelope.
    /// </summary>
    protected ObjectResult PartnershipError(Error error) => PartnershipErrorResults.FromError(error, HttpContext);

    /// <summary>
    /// Returns 429 with the <see cref="PartnershipErrorResults.RateLimitedCode"/> code.
    /// </summary>
    protected ObjectResult RateLimited() => PartnershipErrorResults.RateLimited(HttpContext);
}
