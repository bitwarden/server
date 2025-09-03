# Push

## About

Push is a feature for sending packets of information to end user devices. This can be useful for
telling the device that there is new information that it should request or that the request they
created was just accepted.

## Usage

The general usage will be to call `Bit.Core.Platform.Push.IPushNotificationService.PushAsync`. That
method takes a `PushNotification<T>`.

```c#
// This would send a notification to all the devices of the given `userId`. 
await pushNotificationService.PushAsync(new PushNotification<MyPayload>
{
    Type = PushType.MyNotificationType,
    Target = NotificationTarget.User,
    TargetId = userId,
    Payload = new MyPayload
    {
        Message = "Request accepted",
    },
    ExcludeCurrentContext = false,
});
```

## Extending

If you want to extend this framework for sending your own notification type you do so by adding a
new enum member to the [`PushType`](./PushType.cs) enum. Assign a number to it that is 1 above the next
highest value. You must then annotate that enum member with a
[`[NotificationInfo]`](./NotificationInfoAttribute.cs) attribute to inform others who the owning
team and expected payload type are. Then you may inject
[`IPushNotificationService`](./IPushNotificationService.cs) into your own service and call its
`PushAsync` method.

You should NOT add tests for your specific notification type in any of the `IPushEngine`
implementations. They do currently have tests for many of the notification types but those will
eventually be deleted and no new ones need to be added.

Since notifications are relayed through our cloud instance for self hosted users (if they opt in) it's
important to try and keep the information in the notification payload minimal. It's generally best
to send a notification with IDs for any entities involved, which mean nothing to our cloud but can then be used to get
more detailed information once the notification is received on the device.

## Implementations

The implementation of push notifications scatters push notification requests to all `IPushEngine`s
that have been registered in DI for the current application. In release builds, this service does
NOT await the underlying engines to make sure that the notification has arrived at its destination
before its returned task completes.

### Azure Notification Hub

Used when the application is hosted by Bitwarden in the cloud. This sends the notification to the
configured Azure Notification Hub, which we currently rely on for sending notifications to:
- Our mobile clients, through the Notification Hub federation with mobile app notification systems, and
- Our clients configured to use Web Push (currently the Chrome Extension).

mobile clients and any clients configured to use Web Push (currently Chrome Extension).

This implementation is always assumed to have available configuration when running in the cloud.

### Azure Queue

Used when the application is hosted by Bitwarden in the cloud, to send the notification over web sockets (SignalR). This sends the notification to a Azure
Queue. That queue is then consumed in our Notifications service, where the notification is sent
to a SignalR hub so that our clients connected through a persistent web socket to our notifications
service get the notification.

This implementation is registered in DI when `GlobalSettings:Notifications:ConnectionString` is set
to a value.

### Relay

Used when the application is being self-hosted. This relays a notification from the self-hosted
instance to a cloud instance. The notification is recieved by our cloud and then relayed to
Azure Notification Hub. This is necessary because self-hosted instance aren't able to directly send
notifications to mobile devices.

This instance is registered in DI when `GlobalSettings:PushRelayBaseUri` and
`GlobalSettings:Installation:Key` are available.

### Notifications API

Used when the application is being self-hosted. This sends a API request to the self-hosted instance
of the Notifications service. The Notifications service receives the request and then sends the
notification through the SignalR hub. This is very similar to cloud using an Azure Queue but it
doesn't require the self-hosted customer to run their own queuing infrastructure.

This instance is registered when `GlobalSettings:InternalIdentityKey` and
`GlobalSettings:BaseServiceUri:InternalNotifications` are set. Both of these settings are usually
set automatically in supported Bitwarden setups.
