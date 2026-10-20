using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.AdminConsole.Partnerships.Models.Request;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

[Authorize(Policies.Organization)]
[Route("partnerships/v1/activation-links")]
public class ActivationLinksController(
    IPartnershipRequestContext partnershipRequestContext,
    ICreatePartnershipActivationLinkCommand createPartnershipActivationLinkCommand)
    : PartnershipControllerBase
{
    /// <summary>
    /// Mints a short-lived activation link for a provisioned entitlement, or for a canceled one whose binding is
    /// still inside its resume window.
    /// </summary>
    [HttpPost]
    [PartnershipIdempotent]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ActivationLinkResponseModel>> PostAsync([FromBody] CreateActivationLinkRequestModel model)
    {
        var result = await createPartnershipActivationLinkCommand.CreateAsync(new CreatePartnershipActivationLinkRequest
        {
            Partnership = partnershipRequestContext.Partnership,
            ExternalId = model.ExternalId,
            ReturnUrl = model.ReturnUrl,
        });

        return result.Match<ActionResult<ActivationLinkResponseModel>>(
            error => PartnershipError(error),
            link => StatusCode(StatusCodes.Status201Created, new ActivationLinkResponseModel(link)));
    }
}
