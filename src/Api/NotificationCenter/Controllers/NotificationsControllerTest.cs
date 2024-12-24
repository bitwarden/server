#nullable enable
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.NotificationCenter.Controllers;

[Route("notifications/testing-push")]
public class NotificationsControllerTest : Controller
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;

    public NotificationsControllerTest(
        IPushNotificationService pushNotificationService,
        INotificationRepository notificationRepository,
        INotificationStatusRepository notificationStatusRepository)
    {
        _pushNotificationService = pushNotificationService;
        _notificationRepository = notificationRepository;
        _notificationStatusRepository = notificationStatusRepository;
    }

    [HttpPatch("{id}")]
    public async Task TestingPush([FromRoute] Guid id)
    {
        var notification = await _notificationRepository.GetByIdAsync(id);

        await _pushNotificationService.PushNotificationAsync(notification!);
    }

    [HttpPatch("{id}/{userId}")]
    public async Task TestingStatusPush([FromRoute] Guid id, [FromRoute] Guid userId)
    {
        var notification = await _notificationRepository.GetByIdAsync(id);
        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(id, userId);

        await _pushNotificationService.PushNotificationStatusAsync(notification!, notificationStatus!);
    }
}
