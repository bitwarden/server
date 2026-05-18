using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/invite-link")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.GenerateInviteLink)]
public class OrganizationInviteLinksPublicController(
    IGetOrganizationInviteLinkStatusQuery getOrganizationInviteLinkStatusQuery)
    : BaseAdminConsoleController
{
    [AllowAnonymous]
    [HttpGet("{code:guid}/status")]
    public async Task<IResult> GetStatus(Guid code)
    {
        var result = await getOrganizationInviteLinkStatusQuery.GetStatusAsync(code);

        return Handle(result, status =>
            TypedResults.Ok(new OrganizationInviteLinkStatusResponseModel(
                status.OrganizationId,
                status.OrganizationName,
                status.SeatsAvailable,
                status.Sso is null
                    ? null
                    : new OrganizationInviteLinkSsoResponseModel(status.Sso.OrgSsoId, status.Sso.Required))));
    }
}
