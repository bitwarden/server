using Bit.Api.AdminConsole.Partnerships.Idempotency;
using Bit.Api.AdminConsole.Partnerships.Models.Response;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Partnerships.Controllers;

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
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<PartnershipReconciliationExportResponseModel>> PostAsync()
    {
        var export = await createPartnershipReconciliationExportCommand.CreateAsync(
            partnershipRequestContext.Partnership.Id);
        return Accepted(new PartnershipReconciliationExportResponseModel(
            new PartnershipReconciliationExportDetails(export, DownloadUrl: null)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PartnershipReconciliationExportResponseModel>> GetAsync(Guid id)
    {
        var result = await getPartnershipReconciliationExportQuery.GetAsync(
            partnershipRequestContext.Partnership.Id, id);
        return result.Match<ActionResult<PartnershipReconciliationExportResponseModel>>(
            error => PartnershipError(error),
            details => new PartnershipReconciliationExportResponseModel(details));
    }
}
