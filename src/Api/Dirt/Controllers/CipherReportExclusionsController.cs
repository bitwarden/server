using Bit.Core;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("ciphers")]
[Authorize("Application")]
public class CipherReportExclusionsController : Controller
{
    private readonly IUserService _userService;
    private readonly IExcludeCipherFromReportsCommand _excludeCipherFromReportsCommand;

    public CipherReportExclusionsController(
        IUserService userService,
        IExcludeCipherFromReportsCommand excludeCipherFromReportsCommand)
    {
        _userService = userService;
        _excludeCipherFromReportsCommand = excludeCipherFromReportsCommand;
    }

    /// <summary>Excludes the caller's personal login from their health reports.</summary>
    [HttpPut("{id}/exclude-from-reports")]
    [RequireFeature(FeatureFlagKeys.HealthReportExclusions)]
    public async Task PutExcludeFromReportsAsync(Guid id)
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        await _excludeCipherFromReportsCommand.ExcludeAsync(id, userId);
    }
}
