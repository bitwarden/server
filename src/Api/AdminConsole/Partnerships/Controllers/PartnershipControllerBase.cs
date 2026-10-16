using Bit.Core.AdminConsole.Utilities.v2;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

/// <summary>
/// Base class for partner-facing controllers under <see cref="PartnershipRoutes.Prefix"/>. Deriving from it applies
/// <see cref="PartnershipErrorFilter"/> so every error uses the partnership envelope.
/// </summary>
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
