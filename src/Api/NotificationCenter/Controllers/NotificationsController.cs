#nullable enable
using Bit.Api.Models.Response;
using Bit.Api.NotificationCenter.Models.Request;
using Bit.Api.NotificationCenter.Models.Response;
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
    private readonly IGetNotificationsForUserQuery _getNotificationsForUserQuery;
    private readonly IMarkNotificationDeletedCommand _markNotificationDeletedCommand;
    private readonly IMarkNotificationReadCommand _markNotificationReadCommand;

    public NotificationsController(
        IGetNotificationsForUserQuery getNotificationsForUserQuery,
        IMarkNotificationDeletedCommand markNotificationDeletedCommand,
        IMarkNotificationReadCommand markNotificationReadCommand)
    {
        _getNotificationsForUserQuery = getNotificationsForUserQuery;
        _markNotificationDeletedCommand = markNotificationDeletedCommand;
        _markNotificationReadCommand = markNotificationReadCommand;
    }

    [HttpGet("")]
    public async Task<ListResponseModel<NotificationResponseModel>> List(
        [FromQuery] NotificationFilterRequestModel filter)
    {
        var notificationStatusFilter = new NotificationStatusFilter
        {
            Read = filter.ReadStatusFilter,
            Deleted = filter.DeletedStatusFilter
        };

        var notifications = await _getNotificationsForUserQuery.GetByUserIdStatusFilterAsync(notificationStatusFilter);

        var filteredNotifications = notifications
            .Where(n => n.RevisionDate >= filter.Start && n.RevisionDate < filter.End)
            .Take(filter.PageSize);

        var responses = filteredNotifications.Select(n => new NotificationResponseModel(n));
        return new ListResponseModel<NotificationResponseModel>(responses);
    }

    [HttpPatch("{id}/delete")]
    public async Task MarkAsDeleted([FromRoute] Guid id)
    {
        await _markNotificationDeletedCommand.MarkDeletedAsync(id);
    }

    [HttpPatch("{id}/read")]
    public async Task MarkAsRead([FromRoute] Guid id)
    {
        await _markNotificationReadCommand.MarkReadAsync(id);
    }
}
