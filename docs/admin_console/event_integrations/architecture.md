## Design goals

The main goal of event integrations is to easily enable adding new integrations over time without the need
for a lot of custom work to expose events to a new integration. The ability of fan-out offered by AMQP
(either in RabbitMQ or in Azure Service Bus) gives us a way to attach any number of new integrations to the
existing event system without needing to add special handling. By adding a new listener to the existing
pipeline, it gains an independent stream of events without the need for additional broadcast code.

We want to enable robust handling of failures and retries. By utilizing the two-tier approach
([described below](#two-tier-exchange)), we build in support at the service level for retries. When we add
new integrations, they can focus solely on the integration-specific logic and reporting status, with all the
process of retries and delays managed by the messaging system.

Another goal is to not only support this functionality in the cloud version, but offer it as well to
self-hosted instances. RabbitMQ provides a lightweight way for self-hosted instances to tie into the event system
using the same robust architecture for integrations without the need for Azure Service Bus.

Finally, we want to offer organization admins flexibility and control over what events are significant, where
to send events, and the data to be included in the message. The configuration architecture allows Organizations
to customize details of a specific integration; see [Integrations and integration
configurations](#integrations-and-integration-configurations) below for more details on the configuration piece.

## Architecture

The entry point for the event integrations is the `IEventWriteService`. By configuring the
`EventIntegrationEventWriteService` as the `EventWriteService`, all events sent to the
service are broadcast on the RabbitMQ or Azure Service Bus message exchange. To abstract away
the specifics of publishing to a specific AMQP provider, an `IEventIntegrationPublisher`
is injected into `EventIntegrationEventWriteService` to handle the publishing of events to the
RabbitMQ or Azure Service Bus service.

### Two-tier exchange

When `EventIntegrationEventWriteService` publishes, it posts to the first tier of our two-tier
approach to handling messages. Each tier is represented in the AMQP stack by a separate exchange
(in RabbitMQ terminology) or topic (in Azure Service Bus).

See the [EventsProcessor / ASB detailed diagram](/diagrams#Events_Processor_Azure_Service_Bus) for mote info.

#### Event tier

In the first tier, events are broadcast in a fan-out to a series of listeners. The message body
is a JSON representation of an individual `EventMessage` or an array of `EventMessage`. Handlers at
this level are responsible for handling each event or array of events. There are currently two handlers
at this level:
- `EventRepositoryHandler`
    - The `EventRepositoryHandler` is responsible for long term storage of events. It receives all events
      and  stores them via an injected `IEventRepository` into the database.
    - This mirrors the behavior of when event integrations are turned off - cloud stores to Azure Tables
      and self-hosted is stored to the database.
- `EventIntegrationHandler`
    - The `EventIntegrationHandler` is a generic class that is customized to each integration (via the
      configuration details of the integration) and is responsible for determining if there's a configuration
      for this event / organization / integration, fetching that configuration, and parsing the details of the
      event into a template string.
    - The `EventIntegrationHandler` uses the injected `IOrganizationIntegrationConfigurationRepository` to pull
      the specific set of configuration and template based on the event type, organization, and integration type.
      This configuration is what determines if an integration should be sent, what details are necessary for sending
      it, and the actual message to send.
    - The output of `EventIntegrationHandler` is a new `IntegrationMessage`, with the details of this
      the configuration necessary to interact with the integration and the message to send (with all the event
      details incorporated), published to the integration level of the message bus.

#### Integration tier

At the integration level, messages are JSON representations of `IIntegrationMessage` - specifically they
will be concrete types of the generic `IntegrationMessage<T>` where `<T>` is the configuration details of the
specific integration for which they've been sent. These messages represent the details required for
sending a specific event to a specific integration, including handling retries and delays.

Handlers at the integration level are tied directly to the integration (e.g. `SlackIntegrationHandler`,
`WebhookIntegrationHandler`). These handlers take in `IntegrationMessage<T>` and output
`IntegrationHandlerResult`, which tells the listener the outcome of the integration (e.g. success / fail,
if it can be retried and any minimum delay that should occur). This makes them easy to unit test in isolation
without any of the concerns of AMQP or messaging.

The listeners at this level are responsible for firing off the handler when a new message comes in and then
taking the correct action based on the result. Successful results simply acknowledge the message and resolve.
Failures will either be sent to the dead letter queue (DLQ) or re-published for retry after the correct amount of delay.

#### Retries

One of the goals of introducing the integration level is to simplify and enable the process of multiple retries
for a specific event integration. For instance, if a service is temporarily down, we don't want one of our handlers
blocking the rest of the queue while it waits to retry. In addition, we don't want to retry _all_ integrations for a
specific event if only one integration fails nor do we want to re-lookup the configuration details. By splitting
out the `IntegrationMessage<T>` with the configuration, message, and details around retries, we can process each
event / integration individually and retry easily.

When the `IntegrationHandlerResult.Success` is set to `false` (indicating that the integration attempt failed) the
`Retryable` flag tells the listener whether this failure is temporary or final. If the `Retryable` is `false`, then
the message is immediately sent to the DLQ. If it is `true`, the listener uses the `ApplyRetry(DateTime)` method
in `IntegrationMessage` which handles both incrementing the `RetryCount` and updating the `DelayUntilDate` using
the provided DateTime, but also adding exponential backoff (based on `RetryCount`) and jitter. The listener compares
the `RetryCount` in the `IntegrationMessage` to see if it's over the `MaxRetries` defined in Global Settings. If it
is over the `MaxRetries`, the message is sent to the DLQ. Otherwise, it is scheduled for retry.

``` mermaid
flowchart TD
A[Success == false] --> B{Retryable?}
    B -- No --> C[Send to Dead Letter Queue DLQ]
    B -- Yes --> D[Check RetryCount vs MaxRetries]
    D -->|RetryCount >= MaxRetries| E[Send to Dead Letter Queue DLQ]
    D -->|RetryCount < MaxRetries| F[Schedule for Retry]
```

Azure Service Bus supports scheduling messages as part of its core functionality. Retries are scheduled to a specific
time and then ASB holds the message and publishes it at the correct time.

##### RabbitMQ retry options

For RabbitMQ (which will be used by self-host only), we have two different options. The `useDelayPlugin` flag in
`GlobalSettings.RabbitMqSettings` determines which one is used. If it is set to `true`, we use the delay plugin. It
defaults to `false` which indicates we should use retry queues with a timing check.

1. Delay plugin
    - [Delay plugin GitHub repo](https://github.com/rabbitmq/rabbitmq-delayed-message-exchange)
    - This plugin enables a delayed message exchange in RabbitMQ that supports delaying a message for an amount
      of time specified in a special header.
    - This allows us to forego using any retry queues and rely instead on the delay exchange. When a message is
      marked with the header it gets published to the exchange and the exchange handles all the functionality of
      holding it until the appropriate time (similar to ASB's built-in support).
    - The plugin must be setup and enabled before turning this option on (which is why it defaults to off).

2. Retry queues + timing check
    - If the delay plugin setting is off, we push the message to a retry queue which has a fixed amount of time before
      it gets re-published back to the main queue.
    - When a message comes off the queue, we check to see if the `DelayUntilDate` has already passed.
        - If it has passed, we then handle the integration normally and retry the request.
        - If it is still in the future, we put the message back on the retry queue for an additional wait.
    - While this does use extra processing, it gives us better support for honoring the delays even if the delay plugin
      isn't enabled. Since this solution is only intended for self-host, it should be a pretty minimal impact with short
      delays and a small number of retries.

### Listener / Handler pattern

To make it easy to support multiple AMQP services (RabbitMQ and Azure Service Bus), the act
of listening to the stream of messages is decoupled from the act of responding to a message.

#### Listeners

- Listeners handle the details of the communication platform (i.e. RabbitMQ and Azure Service Bus).
- There is one listener for each platform (RabbitMQ / ASB) for each of the two levels - i.e. one event listener
  and one integration listener.
- Perform all the aspects of setup / teardown, subscription, message acknowledgement, etc. for the messaging platform,
  but do not directly process any events themselves. Instead, they delegate to the handler with which they
  are configured.
- Multiple instances can be configured to run independently, each with its own handler and
  subscription / queue.

#### Handlers

- One handler per queue / subscription (e.g. per integration at the integration level).
- Completely isolated from and know nothing of the messaging platform in use. This allows them to be
  freely reused across different communication platforms.
- Perform all aspects of handling an event.
- Allows them to be highly testable as they are isolated and decoupled from the more complicated
  aspects of messaging.

This combination allows for a configuration inside of `ServiceCollectionExtensions.cs` that pairs
instances of the listener service for the currently running messaging platform with any number of
handlers. It also allows for quick development of new handlers as they are focused only on the
task of handling a specific event.

### Publishers and Services

Listeners (and `EventIntegrationHandler`) interact with the messaging system via the `IEventPublisher` interface,
which is backed by a RabbitMQ and ASB specific service. By placing most of the messaging platform details in the
service layer, we are able to handle common things like configuring the connection, binding or creating a specific
queue, etc. in one place. The `IRabbitMqService` and `IAzureServiceBusService` implement the `IEventPublisher`
interface and therefore can also handle directly all the message publishing functionality.

### Integrations and integration configurations

Organizations can configure integration configurations to send events to different endpoints -- each
handler maps to a specific integration and checks for the configuration when it receives an event.
Currently, there are integrations / handlers for Slack, webhooks, and HTTP Event Collector (HEC).

#### `OrganizationIntegration`

- The top-level object that enables a specific integration for the organization.
- Includes any properties that apply to the entire integration across all events.
    - For Slack, it consists of the token: `{ "Token": "xoxb-token-from-slack" }`.
    - For webhooks, it is optional. Webhooks can either be configured at this level or the configuration level,
      but the configuration level takes precedence. However, even though it is optional, an organization must
      have a webhook `OrganizationIntegration` (even will a `null` `Configuration`) to enable configuration
      via `OrganizationIntegrationConfiguration`.
    - For HEC, it consists of the scheme, token, and URI:

```json
    {
      "Scheme": "Bearer",
      "Token": "Auth-token-from-HEC-service",
      "Uri": "https://example.com/api"
    }
```

#### `OrganizationIntegrationConfiguration`

- This contains the configurations specific to each `EventType` for the integration.
- `Configuration` contains the event-specific configuration.
    - For Slack, this would contain what channel to send the message to: `{ "channelId": "C123456" }`
    - For webhooks, this is the URL the request should be sent to: `{ "url": "https://api.example.com" }`
        - Optionally this also can include a `Scheme` and `Token` if this webhook needs Authentication.
        - As stated above, all of this information can be specified here or at the `OrganizationIntegration`
          level, but any properties declared here will take precedence over the ones above.
    - For HEC, this must be null. HEC is configured only at the `OrganizationIntegration` level.
- `Template` contains a template string that is expected to be filled in with the contents of the actual event.
    - The tokens in the string are wrapped in `#` characters. For instance, the UserId would be `#UserId#`.
    - The `IntegrationTemplateProcessor` does the actual work of replacing these tokens with introspected values from
      the provided `EventMessage`.
    - The template does not enforce any structure — it could be a freeform text message to send via Slack, or a
      JSON body to send via webhook; it is simply stored and used as a string for the most flexibility.

#### `OrganizationIntegrationConfigurationDetails`

- This is the combination of both the `OrganizationIntegration` and `OrganizationIntegrationConfiguration` into
  a single object. The combined contents tell the integration's handler all the details needed to send to an
  external service.
- `OrganizationIntegrationConfiguration` takes precedence over `OrganizationIntegration` - any keys present in
  both will receive the value declared in `OrganizationIntegrationConfiguration`.
- An array of `OrganizationIntegrationConfigurationDetails` is what the `EventIntegrationHandler` fetches from
  the database to determine what to publish at the integration level.

### Filtering

In addition to the ability to configure integrations mentioned above, organization admins can
also add `Filters` stored in the `OrganizationIntegrationConfiguration`. Filters are completely
optional and as simple or complex as organization admins want to make them. These are stored in
the database as JSON and serialized into an `IntegrationFilterGroup`. This is then passed to
the `IntegrationFilterService`, which evaluates it to a `bool`. If it's `true`, the integration
proceeds as above. If it's `false`, we ignore this event and do not route it to the integration
level.

#### `IntegrationFilterGroup`

Logical AND / OR grouping of a number of rules and other subgroups.

| Property      | Description                                                                                                                                                                                                                                                                                                                                                      |
|---------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `AndOperator` | Indicates whether **all** (`true`) or **any** (`false`) of the `Rules` and `Groups` must be true. This applies to _both_ the inner group and the list of rules; for instance, if this group contained Rule1 and Rule2 as well as Group1 and Group2:<br/><br/>`true`: `Rule1 && Rule2 && Group1 && Group2`<br>`false`: `Rule1 \|\| Rule2 \|\| Group1 \|\| Group2` |
| `Rules`       | A list of `IntegrationFilterRule`. Can be null or empty, in which case it will return `true`.                                                                                                                                                                                                                                                                    |
| `Groups`      | A list of nested `IntegrationFilterGroup`. Can be null or empty, in which case it will return `true`.                                                                                                                                                                                                                                                            |

#### `IntegrationFilterRule`

The core of the filtering framework to determine if the data in this specific EventMessage
matches the data for which the filter is searching.

| Property    | Description                                                                                                                                                                                                                                                 |
|-------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Property`  | The property on `EventMessage` to evaluate (e.g., `CollectionId`).                                                                                                                                                                                          |
| `Operation` | The comparison to perform between the property and `Value`. <br><br>**Supported operations:**<br>• `Equals`: `Guid` equals `Value`<br>• `NotEquals`: logical inverse of `Equals`<br>• `In`: `Guid` is in `Value` list<br>• `NotIn`: logical inverse of `In` |
| `Value`     | The comparison value. Type depends on `Operation`: <br>• `Equals`, `NotEquals`: `Guid`<br>• `In`, `NotIn`: list of `Guid`                                                                                                                                   |
