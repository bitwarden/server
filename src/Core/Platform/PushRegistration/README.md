# Push Registration

## About

Push Registration is a feature for managing devices that should receive push notifications. The main
entrypoint for this feature is `IPushRegistrationService`. 

## Usage

This feature is largely used internally to Platform owned endpoints or in concert with another team.

If your feature changes the status of any of the following pieces of data please contact Platform
so that we can keep push registration working correctly.

- The creation/deletion of a new `Device`.
- The addition of removal of an organization a `User` is a part of.

## Implementation

### Azure Notification Hub

Used when the application is hosted by Bitwarden in the cloud. This registers the device and
associated metadata with Azure Notification Hub (ANH). This is necessary so that when a notification
is sent ANH will be able to get the notification to that device.

Since Azure Notification Hub has a limit on the amount of devices per hub we have begun to shard
devices across multiple hubs. Multiple hubs can be configured through configuration and each one can
have a `RegistrationStartDate` and `RegistrationEndDate`. If the start date is `null` no devices
will be registered against that given hub. A `null` end date is treated as no known expiry. The
creation date for a device is retrieved by the device's ID, and that date is used to find a hub that spans
during it's creation date.

When we register a device with Azure Notification Hub we include tags, which are data that can later
be used to specifically target that device with a notification. We send the ID of the user this
device belongs to, the type of the client (Web, Desktop, Mobile, etc), all the organization IDs of
organizations of which the user is a confirmed member, the ID of the self-hosted installation if this
device was relayed to us, and the device identifier, which is a random GUID generated on the device.
Most of this data is considered immutable after the creation of a device, except for the
organization memberships of a user. If a user is added/removed from an organization, it is important
that `CreateOrUpdateRegistrationAsync` is called with the new memberships.

### Relay

Used when the application is self-hosted. This sends a API request to the configured cloud instance
and which will then use [Azure Notification Hub](#azure-notification-hub) but will associate the
installation as the self-hosted installation id instead of using the cloud one.
