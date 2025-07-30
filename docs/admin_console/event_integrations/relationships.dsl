# Top Level event publishing
server.api -> server.azure_service_bus.event_topic "Sends events to"
server.events -> server.azure_service_bus.event_topic "Sends events to"
server.api -> server.rabbit_mq.event_exchange "Sends events to"
server.events -> server.rabbit_mq.event_exchange "Sends events to"

# Azure Service Bus topics, subscriptions, and routing
server.azure_service_bus.event_topic -> server.azure_service_bus.eventsWriteSub  "Fan-out Subscription"
server.azure_service_bus.event_topic -> server.azure_service_bus.eventsHecSub "Fan-out Subscription"
server.azure_service_bus.event_topic -> server.azure_service_bus.eventsWebhookSub  "Fan-out Subscription"
server.azure_service_bus.event_topic -> server.azure_service_bus.eventsSlackSub "Fan-out Subscription"
server.azure_service_bus.integration_topic -> server.azure_service_bus.integrationSlackSub "Subscription filtered via Slack key"
server.azure_service_bus.integration_topic -> server.azure_service_bus.integrationWebhookSub "Subscription filtered via Webhook key"
server.azure_service_bus.integration_topic -> server.azure_service_bus.integrationHecSub "Subscription filtered via HEC key"

# EventsProcessor handling of topics/subscriptions
server.azure_service_bus.eventsWriteSub -> server.events_processor.event_repository_handler "EventListenerService"
server.azure_service_bus.eventsHecSub -> server.events_processor.event_integration_handler "EventListenerService"
server.azure_service_bus.eventsSlackSub -> server.events_processor.event_integration_handler "EventListenerService"
server.azure_service_bus.eventsWebhookSub -> server.events_processor.event_integration_handler "EventListenerService"

server.events_processor.event_integration_handler -> server.azure_service_bus.integration_topic "Publishes To"
server.events_processor.event_integration_handler -> server.events_processor.integration_configuration_details_cache_service "Fetches configurations from"
server.events_processor.integration_configuration_details_cache_service -> server.database "Fetches configurations from"
server.events_processor.event_integration_handler -> server.events_processor.integration_filter_service "Runs filters"
server.events_processor.event_repository_handler -> server.database

server.azure_service_bus.integrationSlackSub -> server.events_processor.slack_integration_handler "IntegrationListenerService"
server.azure_service_bus.integrationWebhookSub -> server.events_processor.webhook_integration_handler "IntegrationListenerService"
server.azure_service_bus.integrationHecSub -> server.events_processor.webhook_integration_handler "IntegrationListenerService"

# RabbitMQ exchanges, queues, and routing
server.rabbit_mq.event_exchange -> server.rabbit_mq.eventsWriteQueue  "Fan-out Subscription"
server.rabbit_mq.event_exchange -> server.rabbit_mq.eventsHecQueue "Fan-out Subscription"
server.rabbit_mq.event_exchange -> server.rabbit_mq.eventsWebhookQueue  "Fan-out Subscription"
server.rabbit_mq.event_exchange -> server.rabbit_mq.eventsSlackQueue "Fan-out Subscription"
server.rabbit_mq.integration_exchange -> server.rabbit_mq.integrationSlackQueue "Routed via Slack key"
server.rabbit_mq.integration_exchange -> server.rabbit_mq.integrationWebhookQueue "Routed via Webhook key"
server.rabbit_mq.integration_exchange -> server.rabbit_mq.integrationHecQueue "Routed via HEC key"
server.rabbit_mq.integrationSlackRetryQueue -> server.rabbit_mq.integrationSlackQueue "DLQ after configured retry timing"
server.rabbit_mq.integrationWebhookRetryQueue -> server.rabbit_mq.integrationWebhookQueue "DLQ after configured retry timing"
server.rabbit_mq.integrationHecRetryQueue -> server.rabbit_mq.integrationHecQueue "DLQ after configured retry timing"

server.rabbit_mq.eventsWriteQueue -> server.events.event_repository_handler "EventListenerService"
server.rabbit_mq.eventsHecQueue -> server.events.event_integration_handler "EventListenerService"
server.rabbit_mq.eventsWebhookQueue -> server.events.event_integration_handler "EventListenerService"
server.rabbit_mq.eventsSlackQueue -> server.events.event_integration_handler "EventListenerService"
server.events.event_integration_handler -> server.rabbit_mq.integration_exchange "Publishes To"
server.rabbit_mq.integrationSlackQueue -> server.events.slack_integration_handler "IntegrationListenerService"
server.rabbit_mq.integrationWebhookQueue -> server.events.webhook_integration_handler "IntegrationListenerService"
server.rabbit_mq.integrationHecQueue -> server.events.webhook_integration_handler "IntegrationListenerService"

# External Services
server.events_processor.slack_integration_handler -> server.events_processor.slack_service "Uses"
server.events_processor.slack_service  -> external_services.slack "Publishes configured events to"
server.events_processor.webhook_integration_handler -> server.events_processor.http_client "Uses"
server.events_processor.http_client -> external_services.crowdstrike "Publishes configured events to"
server.events_processor.http_client -> external_services.datadog "Publishes configured events to"
server.events_processor.http_client -> external_services.splunk "Publishes configured events to"
