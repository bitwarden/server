using Bit.Api.Tools.Models.Response;
using Bit.Core.Context;
using Bit.Core.Services;
using Bit.Core.Tools.Queries.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Tools.Controllers;

[Route("reports")]
[Authorize("Application")]
public class ReportsController : Controller
{
    private readonly ICurrentContext _currentContext;
    private readonly IGetInactiveTwoFactorQuery _getInactiveTwoFactorQuery;
    private readonly IUserService _userService;

    public ReportsController(ICurrentContext currentContext, IGetInactiveTwoFactorQuery getInactiveTwoFactorQuery, IUserService userService)
    {
        _currentContext = currentContext;
        _getInactiveTwoFactorQuery = getInactiveTwoFactorQuery;
        _userService = userService;
    }

    [HttpGet("inactive-two-factor")]
    public async Task<InactiveTwoFactorResponseModel> GetInactiveTwoFactorAsync()
    {
        // Premium guarded
        var user = await _userService.GetUserByPrincipalAsync(User);
        if (!user.Premium)
        {
            throw new UnauthorizedAccessException("Premium required");
        }

        var services = await _getInactiveTwoFactorQuery.GetInactiveTwoFactorAsync();
        return new InactiveTwoFactorResponseModel()
        {
            Services = services
        };

    }
}
