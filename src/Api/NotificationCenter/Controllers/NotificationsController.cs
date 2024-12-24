#nullable enable
using Bit.Api.Models.Response;
using Bit.Api.NotificationCenter.Models.Request;
using Bit.Api.NotificationCenter.Models.Response;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.NotificationCenter.Controllers;

[Route("notifications")]
[Authorize("Application")]
public class NotificationsController : Controller
{
    private readonly IGetNotificationStatusDetailsForUserQuery _getNotificationStatusDetailsForUserQuery;
    private readonly IMarkNotificationDeletedCommand _markNotificationDeletedCommand;
    private readonly IMarkNotificationReadCommand _markNotificationReadCommand;

    public NotificationsController(
        IGetNotificationStatusDetailsForUserQuery getNotificationStatusDetailsForUserQuery,
        IMarkNotificationDeletedCommand markNotificationDeletedCommand,
        IMarkNotificationReadCommand markNotificationReadCommand)
    {
        _getNotificationStatusDetailsForUserQuery = getNotificationStatusDetailsForUserQuery;
        _markNotificationDeletedCommand = markNotificationDeletedCommand;
        _markNotificationReadCommand = markNotificationReadCommand;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<NotificationResponseModel>> ListAsync(
        [FromQuery] NotificationFilterRequestModel filter)
    {
        var pageOptions = new PageOptions
        {
            ContinuationToken = filter.ContinuationToken,
            PageSize = filter.PageSize
        };

        var notificationStatusFilter = new NotificationStatusFilter
        {
            Read = filter.ReadStatusFilter,
            Deleted = filter.DeletedStatusFilter
        };

        var notificationStatusDetailsPagedResult =
            await _getNotificationStatusDetailsForUserQuery.GetByUserIdStatusFilterAsync(notificationStatusFilter,
                pageOptions);

        var responses = notificationStatusDetailsPagedResult.Data
            .Select(n => new NotificationResponseModel(n))
            .ToList();

        return new ListResponseModel<NotificationResponseModel>(responses,
            notificationStatusDetailsPagedResult.ContinuationToken);
    }

    [HttpPatch("{id}/delete")]
    public async Task MarkAsDeletedAsync([FromRoute] Guid id)
    {
        await _markNotificationDeletedCommand.MarkDeletedAsync(id);
    }

    [HttpPatch("{id}/read")]
    public async Task MarkAsReadAsync([FromRoute] Guid id)
    {
        await _markNotificationReadCommand.MarkReadAsync(id);
    }
}
