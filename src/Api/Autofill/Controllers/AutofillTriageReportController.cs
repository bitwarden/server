using Bit.Api.Autofill.Models;
using Bit.Core;
using Bit.Core.Autofill.Commands;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Autofill.Controllers;

[Route("autofill/triage-report")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.EnableAutofillIssueReporting)]
public class AutofillTriageReportController(ICreateAutofillTriageReportCommand createAutofillTriageReportCommand)
    : Controller
{
    [HttpPost("")]
    public async Task<IActionResult> Post([FromBody] AutofillTriageReportRequestModel model)
    {
        await createAutofillTriageReportCommand.Run(model.ToEntity());
        return NoContent();
    }
}
