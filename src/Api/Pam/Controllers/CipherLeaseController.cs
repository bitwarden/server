using Bit.Api.Pam.Models.Request;
using Bit.Api.Pam.Models.Response;
using Bit.Core;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Pam.Controllers;

[Route("ciphers/{id:guid}/lease")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.Pam)]
public class CipherLeaseController(
    IUserService userService,
    IAccessPreCheckQuery preCheckQuery,
    IRequestAccessCommand requestAccessCommand)
    : Controller
{
    /// <summary>
    /// Reports whether leasing this cipher would be approved automatically or require human approval, so the client
    /// can present the appropriate workflow. No request is created.
    /// </summary>
    [HttpGet("pre-check")]
    public async Task<AccessPreCheckResponseModel> PreCheck(Guid id)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await preCheckQuery.PreCheckAsync(userId, id);
        return new AccessPreCheckResponseModel(id, result);
    }

    /// <summary>
    /// Submits a request to lease this cipher. The automatic path issues an active lease immediately; the human path
    /// creates a pending request for an approver.
    /// </summary>
    [HttpPost("")]
    public async Task<AccessRequestResponseModel> Post(Guid id, [FromBody] AccessRequestModel model)
    {
        var userId = userService.GetProperUserId(User)!.Value;
        var result = await requestAccessCommand.RequestAccessAsync(userId, id, model.ToSubmission());
        return new AccessRequestResponseModel(result);
    }
}
