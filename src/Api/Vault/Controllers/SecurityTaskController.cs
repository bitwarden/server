// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Services;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
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
    private readonly IMarkTaskAsCompleteCommand _markTaskAsCompleteCommand;
    private readonly IGetTasksForOrganizationQuery _getTasksForOrganizationQuery;
    private readonly ICreateManyTasksCommand _createManyTasksCommand;
    private readonly ICreateManyTaskNotificationsCommand _createManyTaskNotificationsCommand;
    private readonly IGetTaskMetricsForOrganizationQuery _getTaskMetricsForOrganizationQuery;

    public SecurityTaskController(
        IUserService userService,
        IGetTaskDetailsForUserQuery getTaskDetailsForUserQuery,
        IMarkTaskAsCompleteCommand markTaskAsCompleteCommand,
        IGetTasksForOrganizationQuery getTasksForOrganizationQuery,
        ICreateManyTasksCommand createManyTasksCommand,
        ICreateManyTaskNotificationsCommand createManyTaskNotificationsCommand,
        IGetTaskMetricsForOrganizationQuery getTaskMetricsForOrganizationQuery)
    {
        _userService = userService;
        _getTaskDetailsForUserQuery = getTaskDetailsForUserQuery;
        _markTaskAsCompleteCommand = markTaskAsCompleteCommand;
        _getTasksForOrganizationQuery = getTasksForOrganizationQuery;
        _createManyTasksCommand = createManyTasksCommand;
        _createManyTaskNotificationsCommand = createManyTaskNotificationsCommand;
        _getTaskMetricsForOrganizationQuery = getTaskMetricsForOrganizationQuery;
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

    /// <summary>
    /// Retrieves security tasks for an organization. Restricted to organization administrators.
    /// </summary>
    /// <param name="organizationId">The organization Id</param>
    /// <param name="status">Optional filter for task status. If not provided, returns tasks of all statuses.</param>
    [HttpGet("organization")]
    public async Task<ListResponseModel<SecurityTasksResponseModel>> ListForOrganization(
        [FromQuery] Guid organizationId, [FromQuery] SecurityTaskStatus? status)
    {
        var securityTasks = await _getTasksForOrganizationQuery.GetTasksAsync(organizationId, status);
        var response = securityTasks.Select(x => new SecurityTasksResponseModel(x)).ToList();
        return new ListResponseModel<SecurityTasksResponseModel>(response);
    }

    /// <summary>
    /// Retrieves security task metrics for an organization.
    /// </summary>
    /// <param name="orgId">The organization Id</param>
    [HttpGet("{orgId:guid}/metrics")]
    public async Task<SecurityTaskMetricsResponseModel> GetTaskMetricsForOrg([FromRoute] Guid orgId)
    {
        var metrics = await _getTaskMetricsForOrganizationQuery.GetTaskMetrics(orgId);

        return new SecurityTaskMetricsResponseModel(metrics.completedTasksCount, metrics.totalTasksCount);
    }

    /// <summary>
    /// Bulk create security tasks for an organization.
    /// </summary>
    /// <param name="orgId"></param>
    /// <param name="model"></param>
    /// <returns>A list response model containing the security tasks created for the organization.</returns>
    [HttpPost("{orgId:guid}/bulk-create")]
    public async Task<ListResponseModel<SecurityTasksResponseModel>> BulkCreateTasks(Guid orgId,
        [FromBody] BulkCreateSecurityTasksRequestModel model)
    {
        // Retrieve existing pending security tasks for the organization
        var pendingSecurityTasks = await _getTasksForOrganizationQuery.GetTasksAsync(orgId, SecurityTaskStatus.Pending);

        // Get the security tasks that are already associated with a cipher within the submitted model
        var existingTasks = pendingSecurityTasks.Where(x => model.Tasks.Any(y => y.CipherId == x.CipherId)).ToList();

        // Get tasks that need to be created
        var tasksToCreateFromModel = model.Tasks.Where(x => !existingTasks.Any(y => y.CipherId == x.CipherId)).ToList();

        ICollection<SecurityTask> newSecurityTasks = new List<SecurityTask>();

        if (tasksToCreateFromModel.Count != 0)
        {
            newSecurityTasks = await _createManyTasksCommand.CreateAsync(orgId, tasksToCreateFromModel);
        }

        // Combine existing tasks and newly created tasks
        var allTasks = existingTasks.Concat(newSecurityTasks);

        await _createManyTaskNotificationsCommand.CreateAsync(orgId, allTasks);

        var response = allTasks.Select(x => new SecurityTasksResponseModel(x)).ToList();
        return new ListResponseModel<SecurityTasksResponseModel>(response);
    }
}
