using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.Auth.Identity;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

[Authorize(Policies.Organization)]
[Route("partnerships/v1/partnership")]
public class PartnershipController(
    IPartnershipRequestContext partnershipRequestContext,
    GlobalSettings globalSettings)
    : PartnershipControllerBase
{
    /// <summary>
    /// Reads the authenticated partner's partnership configuration and rate limit.
    /// </summary>
    [HttpGet("")]
    public ActionResult<PartnershipResponseModel> Get() =>
        new PartnershipResponseModel(partnershipRequestContext.Partnership, globalSettings.Partnerships);
}
