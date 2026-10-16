using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.AdminConsole.Partnerships.Models.Request;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

[Route("partnerships/v1/entitlements")]
public class EntitlementsController(
    IPartnershipRequestContext partnershipRequestContext,
    IProvisionPartnershipEntitlementCommand provisionPartnershipEntitlementCommand,
    IGetPartnershipEntitlementByExternalIdQuery getPartnershipEntitlementByExternalIdQuery,
    ICreatePartnershipActivationLinkCommand createPartnershipActivationLinkCommand)
    : PartnershipControllerBase
{
    /// <summary>
    /// Provisions an entitlement for a customer. Returns 201 when the entitlement is new and 200 when it already
    /// existed, including a canceled one that is provisioned again. Includes an activation link when the entitlement
    /// can be activated.
    /// </summary>
    [HttpPost("")]
    [PartnershipIdempotent]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PartnershipEntitlementResponseModel>> PostAsync(
        [FromBody] ProvisionEntitlementRequestModel model)
    {
        var partnership = partnershipRequestContext.Partnership;
        var result = await provisionPartnershipEntitlementCommand.ProvisionAsync(model.ToRequest(partnership.Id));

        return await result.Match<Task<ActionResult<PartnershipEntitlementResponseModel>>>(
            error => Task.FromResult<ActionResult<PartnershipEntitlementResponseModel>>(PartnershipError(error)),
            async provisioned =>
            {
                var activationLink = await TryCreateActivationLinkAsync(partnership, provisioned.Entitlement);
                var response = new PartnershipEntitlementResponseModel(provisioned, activationLink);
                return provisioned.Created ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
            });
    }

    /// <summary>
    /// Reads an entitlement by the partner's identifier for the customer.
    /// </summary>
    [HttpGet("{externalId}")]
    public async Task<ActionResult<PartnershipEntitlementResponseModel>> GetAsync(string externalId)
    {
        var result = await getPartnershipEntitlementByExternalIdQuery.GetAsync(
            partnershipRequestContext.Partnership.Id, externalId);
        return result.Match<ActionResult<PartnershipEntitlementResponseModel>>(
            error => PartnershipError(error),
            entitlement => new PartnershipEntitlementResponseModel(entitlement));
    }

    /// <summary>
    /// Mints an activation link for a provisioned entitlement, or a canceled one still holding its binding. Returns null
    /// when the entitlement can't be activated; the provisioning already succeeded, so that isn't an error here.
    /// </summary>
    private async Task<PartnershipActivationLink?> TryCreateActivationLinkAsync(
        OrganizationPartnership partnership, OrganizationPartnershipEntitlement entitlement)
    {
        if (entitlement.State is not (PartnershipEntitlementState.Provisioned or PartnershipEntitlementState.Canceled))
        {
            return null;
        }

        var result = await createPartnershipActivationLinkCommand.CreateAsync(new CreatePartnershipActivationLinkRequest
        {
            Partnership = partnership,
            ExternalId = entitlement.ExternalId,
        });
        return result.Match<PartnershipActivationLink?>(_ => null, link => link);
    }
}
