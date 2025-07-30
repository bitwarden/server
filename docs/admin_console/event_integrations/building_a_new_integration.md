# Building a new integration

These are all the pieces required in the process of building out a new integration. For
clarity in naming, these assume a new integration called "Example".

## IntegrationType

Add a new type to `IntegrationType` for the new integration.

## Configuration Models

The configuration models are the classes that will determine what is stored in the database for
`OrganizationIntegration` and `OrganizationIntegrationConfiguration`. The `Configuration` columns are the
serialized version of the corresponding objects and represent the coonfiguration details for this integration
and event type.

1. `ExampleIntegration`
    - Configuration details for the whole integration (e.g. a token in Slack).
    - Applies to every event type configuration defined for this integration.
    - Maps to the JSON structure stored in `Configuration` in ``OrganizationIntegration`.
2. `ExampleIntegrationConfiguration`
    - Configuration details that could change from event to event (e.g. channelId in Slack).
    - Maps to the JSON structure stored in `Configuration` in `OrganizationIntegrationConfiguration`.
3. `ExampleIntegrationConfigurationDetails`
    - Combined configuration of both Integration _and_ IntegrationConfiguration.
    - This will be the deserialized version of the `MergedConfiguration` in
      `OrganizationIntegrationConfigurationDetails`.

## Request Models

1. Add a new case to the switch method in `OrganizationIntegrationRequestModel.Validate`.
2. Add a new case to the switch method in `OrganizationIntegrationConfigurationRequestModel.IsValidForType`.

## Integration Handler

e.g. `ExampleIntegrationHandler`
- This is where the actual code will go to perform the integration (i.e. send an HTTP request, etc.).
- Handlers receive an `IntegrationMessage<T>` where `<T>` is the `ExampleIntegrationConfigurationDetails`
  defined above. This has the Configuration as well as the rendered template message to be sent.
- Handlers return an `IntegrationHandlerResult` with details about if the request - success / failure,
  if it can be retried, when it should be delayed until, etc.
- The scope of the handler is simply to do the integration and report the result.
  Everything else (such as how many times to retry, when to retry, what to do with failures)
  is done in the Listener.

## GlobalSettings

### RabbitMQ
Add the queue names for the integration. These are typically set with a default value so
that they will be created when first accessed in code by RabbitMQ.

1. `ExampleEventQueueName`
2. `ExampleIntegrationQueueName`
3. `ExampleIntegrationRetryQueueName`

### Azure Service Bus
Add the subscription names to use for ASB for this integration. Similar to RabbitMQ a
default value is provided so that we don't require configuring it in secrets but allow
it to be overridden. **However**, unlike RabbitMQ these subscriptions must exist prior
to the code accessing them. They will not be created on the fly. See [Deploying a new
integration](#deploying-a-new-integration) below

1. `ExmpleEventSubscriptionName`
2. `ExmpleIntegrationSubscriptionName`

#### Service Bus Emulator, local config
In order to create ASB resources locally, we need to also update the `servicebusemulator_config.json` file
to include any new subscriptions.
- Under the existing event topic (`event-logging`) add a subscription for the event level for this
  new integration (`events-example-subscription`).
- Under the existing integration topic (`event-integrations`) add a new subscription for the integration
  level messages (`integration-example-subscription`).
    - Copy the correlation filter from the other integration level subscriptions. It should filter based on
      the `IntegrationType.ToRoutingKey`, or in this example `example`.

These names added here are what must match the values provided in the secrets or the defaults provided
in Global Settings. This must be in place (and the local ASB emulator restarted) before you can use any
code locally that accesses ASB resources.

## ServiceCollectionExtensions
In our `ServiceCollectionExtensions`, we pull all the above pieces together to start listeners on each message
tier with handlers to process the integration. There are a number of helper methods in here to make this simple
to add a new integration - one call per platform.

Also note that if an integration needs a custom singleton / service defined, the add listeners method is a
good place to set that up. For instance, `SlackIntegrationHandler` needs a `SlackService`, so the singleton
declaration is right above the add integration method for slack. Same thing for webhooks when it comes to
defining a custom HttpClient by name.

1. In `AddRabbitMqListeners` add the integration:
``` csharp
        services.AddRabbitMqIntegration<ExampleIntegrationConfigurationDetails, ExampleIntegrationHandler>(
            globalSettings.EventLogging.RabbitMq.ExampleEventsQueueName,
            globalSettings.EventLogging.RabbitMq.ExampleIntegrationQueueName,
            globalSettings.EventLogging.RabbitMq.ExampleIntegrationRetryQueueName,
            globalSettings.EventLogging.RabbitMq.MaxRetries,
            IntegrationType.Example);
```

2. In `AddAzureServiceBusListeners` add the integration:
``` csharp
services.AddAzureServiceBusIntegration<ExampleIntegrationConfigurationDetails, ExampleIntegrationHandler>(
            eventSubscriptionName: globalSettings.EventLogging.AzureServiceBus.ExampleEventSubscriptionName,
            integrationSubscriptionName: globalSettings.EventLogging.AzureServiceBus.ExampleIntegrationSubscriptionName,
            integrationType: IntegrationType.Example,
            globalSettings: globalSettings);
```

# Deploying a new integration

## RabbitMQ

RabbitMQ dynamically creates queues and exchanges when they are first accessed in code.
Therefore, there is no need to manually create queues when deploying a new integration.
They can be created and configured ahead of time, but it's not required. Note that once
they are created, if any configurations need to be changed, the queue or exchange must be
deleted and recreated.

## Azure Service Bus

Unlike RabbitMQ, ASB resources **must** be allocated before the code accesses them and
will not be created on the fly. This means that any subscriptions needed for a new
integration must be created in ASB before that code is deployed.

The two subscriptions created above in Global Settings and `servicebusemulator_config.json`
need to be created in the Azure portal or CLI for the environment before deploying the
code.

1. `ExmpleEventSubscriptionName`
    - This subscription is a fan-out subscription from the main event topic.
    - As such, it will start receiving all the events as soon as it is declared.
    - This can create a backlog before the integration-specific handler is declared and deployed.
    - One strategy to avoid this is to create the subscription with a false filter (e.g. `1 = 0`).
        - This will create the subscription, but the filter will ensure that no messages
          actually land in the subscription.
        - Code can be deployed that references the subscription, because the subscription
          legitimately exists (it is simply empty).
        - When the code is in place, and we're ready to start receiving messages on the new
          integration, we simply remove the filter to return the subscription to receiving
          all messages via fan-out.
2. `ExmpleIntegrationSubscriptionName`
    - This subscription must be created before the new integration code can be deployed.
    - However, it is not fan-out, but rather a filter based on the `IntegrationType.ToRoutingKey`.
    - Therefore, it won't start receiving messages until organizations have active configurations.
      This means there's no risk of building up a backlog by declaring it ahead of time.
