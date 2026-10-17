using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.Auth.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

[Authorize(Policies.Organization)]
[Route("partnerships/v1/reconciliation-exports")]
public class PartnershipReconciliationExportsController(
    IPartnershipRequestContext partnershipRequestContext,
    ICreatePartnershipReconciliationExportCommand createPartnershipReconciliationExportCommand,
    IGetPartnershipReconciliationExportQuery getPartnershipReconciliationExportQuery)
    : PartnershipControllerBase
{
    /// <summary>
    /// Starts an export of all of the partnership's entitlements. Poll the returned export until it completes.
    /// </summary>
    [HttpPost("")]
    [PartnershipIdempotent]
    [ProducesResponseType(typeof(PartnershipReconciliationExportResponseModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PartnershipErrorResponseModel), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PartnershipReconciliationExportResponseModel>> PostAsync()
    {
        var export = await createPartnershipReconciliationExportCommand.CreateAsync(
            partnershipRequestContext.Partnership.Id);
        return Accepted(new PartnershipReconciliationExportResponseModel(
            new PartnershipReconciliationExportDetails(export, DownloadUrl: null)));
    }

    /// <summary>
    /// Reads an export's status. Includes a short-lived <c>downloadUrl</c> once it has completed, until it expires.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PartnershipReconciliationExportResponseModel>> GetAsync(Guid id)
    {
        var result = await getPartnershipReconciliationExportQuery.GetAsync(
            partnershipRequestContext.Partnership.Id, id);
        return result.Match<ActionResult<PartnershipReconciliationExportResponseModel>>(
            error => PartnershipError(error),
            details => new PartnershipReconciliationExportResponseModel(details));
    }
}
