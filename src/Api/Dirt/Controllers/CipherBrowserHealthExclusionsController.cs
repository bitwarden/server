using Bit.Core;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Services;
using Bitwarden.Server.Sdk.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Dirt.Controllers;

[Route("ciphers")]
[Authorize("Application")]
public class CipherBrowserHealthExclusionsController : Controller
{
    private readonly IUserService _userService;
    private readonly IExcludeCipherFromBrowserHealthCommand _excludeCipherFromBrowserHealthCommand;

    public CipherBrowserHealthExclusionsController(
        IUserService userService,
        IExcludeCipherFromBrowserHealthCommand excludeCipherFromBrowserHealthCommand)
    {
        _userService = userService;
        _excludeCipherFromBrowserHealthCommand = excludeCipherFromBrowserHealthCommand;
    }

    /// <summary>Excludes the caller's personal login from browser health.</summary>
    [HttpPut("{id}/exclude-from-browser-health")]
    [RequireFeature(FeatureFlagKeys.HealthReportExclusions)]
    public async Task PutExcludeFromBrowserHealthAsync(Guid id)
    {
        var userId = _userService.GetProperUserId(User)!.Value;
        await _excludeCipherFromBrowserHealthCommand.ExcludeAsync(id, userId);
    }
}
