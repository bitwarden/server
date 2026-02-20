# Push Registration

## About

Push Registration is a feature for managing devices that should receive push notifications. The main
entrypoint for this feature is `IPushRegistrationService`.

## Usage

This feature is largely used internally to Platform owned endpoints or in concert with another team.

If your feature changes the status of any of the following pieces of data please contact Platform
so that we can keep push registration working correctly:

- The creation/deletion of a new `Device`.
- The addition or removal of an organization a `User` is a part of.

## Implementation

### Azure Notification Hub

Used when the application is hosted by Bitwarden in the cloud. This registers the device and
associated metadata with [Azure Notification Hub (ANH)](https://learn.microsoft.com/en-us/azure/notification-hubs/notification-hubs-push-notification-overview).
This is necessary so that when a notification is sent ANH will be able to get the notification to
that device.

Since Azure Notification Hub has a limit on the amount of devices per hub we have begun to shard
devices across multiple hubs. Multiple hubs can be setup through configuration and each one can
have a `RegistrationStartDate` and `RegistrationEndDate`. If the start date is `null` no devices
will be registered against that given hub. A `null` end date is treated as no known expiry. The
creation date for a device is retrieved by the device's ID, and that date is used to find a hub that
was actively accepting registrations on that device's creation date. When a new notification hub
pool entry is a `RegistrationEndDate` should be added for the previous pool entry. The end date
added to the previous entry should be equal to the start date of the new entry. Both of these dates
should be in the future relative to the release date of the release they are going to be added as a
part of. This way the release can happen and any current in flight devices will continue to be
registered with the previous entry and once the release has completed and had a little time to
settle we can start registering devices on the new notification hub. An overlap of one entries end
date and another entries start date would be preferable to not having them be equal or no overlap.

Notification hub pool example settings:

```json
{
  "GlobalSettings": {
    "NotificationHubPool": {
      "NotificationHubs": [
        {
          "HubName": "first",
          "ConnectionString": "anh-connection-string-1",
          "EnableSendTracing": true,
          "RegistrationStartDate": "1900-01-01T00:00:00.0000000Z",
          "RegistrationEndDate": "2025-01-01T00:00:00.0000000Z"
        },
        {
          "HubName": "second",
          "ConnectionString": "anh-connection-string-2",
          "EnableSendTracing": false,
          "RegistrationStartDate": "2025-01-01T00:00:00.0000000Z",
          "RegistrationEndDate": null
        }
      ]
    }
  }
}
```

When we register a device with Azure Notification Hub we include the following tags:

- User ID
- Client Type (Web, Desktop, Mobile, etc)
- Organization IDs of which the user is a confirmed member
- ID of the self-hosted installation if this device was relayed to us
- Device identifier

These tags are used to specifically target a device based on those tags with a notification. Most of
this data is considered immutable after the creation of a device, except for the organization
memberships of a user. If a user is added/removed from an organization, it is important that
`CreateOrUpdateRegistrationAsync` is called with the new memberships.

### Relay

Used when the application is self-hosted. This sends a API request to the configured cloud instance,
which will then use [Azure Notification Hub](#azure-notification-hub) but will associate the
installation as the self-hosted installation ID instead of using the cloud one. The endpoints are
in the [`PushController`](../../../Api/Platform/Push/Controllers/PushController.cs)

### SignalR

While not an implementation of `IPushRegistrationService`, the SignalR hub adds users to various
groups in [`NotificationsHub.OnConnectedAsync`](../../../Notifications/NotificationsHub.cs) method.
It utilizes a manual build of `ICurrentContext` where it reads the claims provided from the access
token.
