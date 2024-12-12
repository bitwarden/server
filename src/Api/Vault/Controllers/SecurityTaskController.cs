using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Vault.Controllers;

[Route("tasks")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.SecurityTasks)]
public class SecurityTaskController : Controller
{
    private readonly IUserService _userService;
    private readonly IGetTaskDetailsForUserQuery _getTaskDetailsForUserQuery;
    private readonly IMarkTaskAsCompleteCommand _markTaskAsCompleteCommand;

    public SecurityTaskController(
        IUserService userService,
        IGetTaskDetailsForUserQuery getTaskDetailsForUserQuery,
        IMarkTaskAsCompleteCommand markTaskAsCompleteCommand)
    {
        _userService = userService;
        _getTaskDetailsForUserQuery = getTaskDetailsForUserQuery;
        _markTaskAsCompleteCommand = markTaskAsCompleteCommand;
    }

    /// <summary>
    /// Retrieves security tasks for the current user.
    /// </summary>
    /// <param name="status">Optional filter for task status. If not provided returns tasks of all statuses.</param>
    /// <returns>A list response model containing the security tasks for the user.</returns>
    [HttpGet("")]
    public async Task<ListResponseModel<SecurityTasksResponseModel>> Get([FromQuery] SecurityTaskStatus? status)
    {
        var userId = _userService.GetProperUserId(User).Value;
        var securityTasks = await _getTaskDetailsForUserQuery.GetTaskDetailsForUserAsync(userId, status);
        var response = securityTasks.Select(x => new SecurityTasksResponseModel(x)).ToList();
        return new ListResponseModel<SecurityTasksResponseModel>(response);
    }

    /// <summary>
    /// Marks a task as complete. The user must have edit permission on the cipher associated with the task.
    /// </summary>
    /// <param name="taskId">The unique identifier of the task to complete</param>
    [HttpPatch("{taskId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid taskId)
    {
        await _markTaskAsCompleteCommand.CompleteAsync(taskId);
        return NoContent();
    }
}
