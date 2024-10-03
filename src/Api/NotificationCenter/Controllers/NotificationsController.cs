#nullable enable
using System.Text.Json;
using Bit.Api.Models.Response;
using Bit.Api.NotificationCenter.Models;
using Bit.Api.NotificationCenter.Models.Request;
using Bit.Api.NotificationCenter.Models.Response;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.Utilities;
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
        var continuationToken = ParseContinuationToken(filter.ContinuationToken);
        var notificationStatusFilter = new NotificationStatusFilter
        {
            Read = filter.ReadStatusFilter,
            Deleted = filter.DeletedStatusFilter
        };

        var notifications = await _getNotificationsForUserQuery.GetByUserIdStatusFilterAsync(notificationStatusFilter);

        if (continuationToken != null)
        {
            notifications = notifications
                .Where(n => n.Priority <= continuationToken.Priority && n.RevisionDate < continuationToken.Date);
        }

        var responses = notifications
            .Take(10)
            .Select(n => new NotificationResponseModel(n))
            .ToList();

        var nextContinuationToken = responses.Count > 0 && responses.Count < 10
            ? new NotificationContinuationToken
            {
                Priority = responses.Last().Priority,
                Date = responses.Last().Date
            }
            : null;

        return new ListResponseModel<NotificationResponseModel>(responses,
            CreateContinuationToken(nextContinuationToken));
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

    private NotificationContinuationToken? ParseContinuationToken(string? continuationToken)
    {
        if (continuationToken == null)
        {
            return null;
        }

        var decodedContinuationToken = CoreHelpers.Base64UrlDecodeString(continuationToken);
        return JsonSerializer.Deserialize<NotificationContinuationToken>(decodedContinuationToken,
            JsonHelpers.IgnoreCase);
    }

    private string? CreateContinuationToken(NotificationContinuationToken? notificationContinuationToken)
    {
        if (notificationContinuationToken == null)
        {
            return null;
        }

        var serializedContinuationToken = JsonSerializer.Serialize(notificationContinuationToken);
        return CoreHelpers.Base64UrlEncodeString(serializedContinuationToken);
    }
}
