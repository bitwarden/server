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
    private readonly IGetNotificationStatusDetailsForUserQuery _getNotificationStatusDetailsForUserQuery;
    private readonly IMarkNotificationDeletedCommand _markNotificationDeletedCommand;
    private readonly IMarkNotificationReadCommand _markNotificationReadCommand;

    private const int RowsPerPage = 10;

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
    public async Task<ListResponseModel<NotificationResponseModel>> List(
        [FromQuery] NotificationFilterRequestModel filter)
    {
        var continuationToken = ParseContinuationToken(filter.ContinuationToken);
        var notificationStatusFilter = new NotificationStatusFilter
        {
            Read = filter.ReadStatusFilter,
            Deleted = filter.DeletedStatusFilter
        };

        var notificationStatusDetails =
            await _getNotificationStatusDetailsForUserQuery.GetByUserIdStatusFilterAsync(notificationStatusFilter);

        if (continuationToken != null)
        {
            // Priority and CreationDate are always in descending order
            notificationStatusDetails = notificationStatusDetails
                .Where(n => n.Priority < continuationToken.Priority ||
                            (n.Priority == continuationToken.Priority && n.CreationDate < continuationToken.Date));
        }

        var pagedNotificationStatusDetails = notificationStatusDetails
            .Take(RowsPerPage)
            .ToList();

        var responses = pagedNotificationStatusDetails
            .Select(n => new NotificationResponseModel(n))
            .ToList();

        var nextContinuationToken = pagedNotificationStatusDetails.Count == RowsPerPage
            ? new NotificationContinuationToken
            {
                Priority = pagedNotificationStatusDetails.Last().Priority,
                Date = pagedNotificationStatusDetails.Last().CreationDate
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

    private static NotificationContinuationToken? ParseContinuationToken(string? continuationToken)
    {
        if (continuationToken == null)
        {
            return null;
        }

        var decodedContinuationToken = CoreHelpers.Base64UrlDecodeString(continuationToken);
        return JsonSerializer.Deserialize<NotificationContinuationToken>(decodedContinuationToken,
            JsonHelpers.CamelCase);
    }

    private static string? CreateContinuationToken(NotificationContinuationToken? notificationContinuationToken)
    {
        if (notificationContinuationToken == null)
        {
            return null;
        }

        var serializedContinuationToken = JsonSerializer.Serialize(notificationContinuationToken,
            JsonHelpers.CamelCase);
        return CoreHelpers.Base64UrlEncodeString(serializedContinuationToken);
    }
}
