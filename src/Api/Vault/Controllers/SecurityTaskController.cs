using Bit.Api.Models.Response;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.Repositories;
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
    private readonly IGetTasksForOrganizationQuery _getTasksForOrganizationQuery;
    private readonly ICreateManyTasksCommand _createManyTasksCommand;
    private readonly IGetSecurityTasksNotificationDetailsQuery _getSecurityTasksNotificationDetailsQuery;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMailService _mailService;
    private readonly ICreateNotificationCommand _createNotificationCommand;

    public SecurityTaskController(
        IUserService userService,
        IGetTaskDetailsForUserQuery getTaskDetailsForUserQuery,
        IMarkTaskAsCompleteCommand markTaskAsCompleteCommand,
        IGetTasksForOrganizationQuery getTasksForOrganizationQuery,
        ICreateManyTasksCommand createManyTasksCommand,
        IGetSecurityTasksNotificationDetailsQuery getSecurityTasksNotificationDetailsQuery,
        IOrganizationRepository organizationRepository,
        IMailService mailService,
        ICreateNotificationCommand createNotificationCommand)
    {
        _userService = userService;
        _getTaskDetailsForUserQuery = getTaskDetailsForUserQuery;
        _markTaskAsCompleteCommand = markTaskAsCompleteCommand;
        _getTasksForOrganizationQuery = getTasksForOrganizationQuery;
        _createManyTasksCommand = createManyTasksCommand;
        _getSecurityTasksNotificationDetailsQuery = getSecurityTasksNotificationDetailsQuery;
        _organizationRepository = organizationRepository;
        _mailService = mailService;
        _createNotificationCommand = createNotificationCommand;
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
    /// Bulk create security tasks for an organization.
    /// </summary>
    /// <param name="orgId"></param>
    /// <param name="model"></param>
    /// <returns>A list response model containing the security tasks created for the organization.</returns>
    [HttpPost("{orgId:guid}/bulk-create")]
    public async Task<ListResponseModel<SecurityTasksResponseModel>> BulkCreateTasks(Guid orgId,
        [FromBody] BulkCreateSecurityTasksRequestModel model)
    {
        var securityTasks = await _createManyTasksCommand.CreateAsync(orgId, model.Tasks);

        var userTaskCount = await _getSecurityTasksNotificationDetailsQuery.GetNotificationDetailsByManyIds(orgId, securityTasks);
        var organization = await _organizationRepository.GetByIdAsync(orgId);
        await _mailService.SendBulkSecurityTaskNotificationsAsync(organization.Name, userTaskCount);
        foreach (var task in userTaskCount)
        {
            var notification = new Notification
            {
                UserId = task.UserId,
                OrganizationId = orgId,
                Priority = Priority.Informational,
                ClientType = ClientType.Browser,
            };
            await _createNotificationCommand.CreateAsync(notification);
        }

        var response = securityTasks.Select(x => new SecurityTasksResponseModel(x)).ToList();
        return new ListResponseModel<SecurityTasksResponseModel>(response);
    }
}
