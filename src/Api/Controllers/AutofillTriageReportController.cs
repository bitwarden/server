using Bit.Api.Models.Request;
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Controllers;

[Route("autofill/triage-report")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.EnableAutofillIssueReporting)]
public class AutofillTriageReportController : Controller
{
    private readonly IAutofillTriageReportRepository _repo;

    public AutofillTriageReportController(IAutofillTriageReportRepository repo)
        => _repo = repo;

    [HttpPost("")]
    public async Task<IActionResult> Post([FromBody] AutofillTriageReportRequestModel model)
    {
        var entity = model.ToEntity();
        entity.SetNewId();
        await _repo.CreateAsync(entity);
        return NoContent();
    }
}
