#nullable enable
using Bit.Api.Models.Response;
using Bit.Api.NotificationCenter.Models.Request;
using Bit.Api.NotificationCenter.Models.Response;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Commands.Interfaces;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Queries.Interfaces;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Services;
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
    private readonly IPushNotificationService _pushNotificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;

    public NotificationsController(
        IGetNotificationStatusDetailsForUserQuery getNotificationStatusDetailsForUserQuery,
        IMarkNotificationDeletedCommand markNotificationDeletedCommand,
        IMarkNotificationReadCommand markNotificationReadCommand,
        IPushNotificationService pushNotificationService,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository)
    {
        _getNotificationStatusDetailsForUserQuery = getNotificationStatusDetailsForUserQuery;
        _markNotificationDeletedCommand = markNotificationDeletedCommand;
        _markNotificationReadCommand = markNotificationReadCommand;
        _pushNotificationService = pushNotificationService;
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
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

    [HttpPatch("testing-push/{id}")]
    public async Task TestingPush([FromRoute] Guid id)
    {
        var notification = await _notificationRepository.GetByIdAsync(id);

        await _pushNotificationService.PushNotificationAsync(notification!);
    }

    [HttpPatch("testing-push/{id}/{userId}")]
    public async Task TestingStatusPush([FromRoute] Guid id, [FromRoute] Guid userId)
    {
        var notification = await _notificationRepository.GetByIdAsync(id);
        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(id, userId);

        await _pushNotificationService.PushNotificationStatusAsync(notification!, notificationStatus!);
    }
}
