using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Services;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

[Route("tasks")]
[Authorize("Application")]
public class SecurityTaskController : Controller
{
    private readonly IUserService _userService;
    private readonly IGetTaskDetailsForUserQuery _getTaskDetailsForUserQuery;

    public SecurityTaskController(IUserService userService, IGetTaskDetailsForUserQuery getTaskDetailsForUserQuery)
    {
        _userService = userService;
        _getTaskDetailsForUserQuery = getTaskDetailsForUserQuery;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<SecurityTasksResponseModel>> Get([FromQuery] IEnumerable<SecurityTaskStatus> status = null)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var securityTasks = await _getTaskDetailsForUserQuery.GetTaskDetailsForUserAsync(userId, status);
        var response = securityTasks.Select(x => new SecurityTasksResponseModel(x)).ToList();
        return new ListResponseModel<SecurityTasksResponseModel>(response);
    }
}
