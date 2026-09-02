using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push;

/// <summary>
/// Used to Push notifications to end-user devices.
/// </summary>
/// <remarks>
/// New notifications should not be wired up inside this service. You may either directly call the
/// <see cref="PushAsync"/> method in your service to send your notification or if you want your notification
/// sent by other teams you can make an extension method on this service with a well typed definition
/// of your notification. You may also make your own service that injects this and exposes methods for each of
/// your notifications.
/// </remarks>
public interface IPushNotificationService
{
    private const string ServiceDeprecation = "Do not use the services exposed here, instead use your own services injected in your service.";

    [Obsolete(ServiceDeprecation, DiagnosticId = "BWP0001")]
    Guid InstallationId { get; }

    [Obsolete(ServiceDeprecation, DiagnosticId = "BWP0001")]
    TimeProvider TimeProvider { get; }

    [Obsolete(ServiceDeprecation, DiagnosticId = "BWP0001")]
    ILogger Logger { get; }

    /// <summary>
    /// Pushes a notification to devices based on the settings given to us in <see cref="PushNotification{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the payload to be sent along with the notification.</typeparam>
    /// <param name="pushNotification"></param>
    /// <returns>A task that is NOT guarunteed to have sent the notification by the time the task resolves.</returns>
    Task PushAsync<T>(PushNotification<T> pushNotification)
        where T : class;
}
