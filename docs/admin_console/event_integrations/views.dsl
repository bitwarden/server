component server.azure_service_bus "Azure_Service_Bus" {
    include *
}

component server.rabbit_mq "RabbitMQ" {
    include *
}

component server.events_processor "Events_Processor" {
    include *
}

dynamic server.events_processor "Events_Processor_Azure_Service_Bus" "Event Integrations / ASB Detail" {
    server.azure_service_bus.event_topic -> server.azure_service_bus.eventsSlackSub "Fan-out Subscription"
    server.azure_service_bus.event_topic -> server.azure_service_bus.eventsHecSub "Fan-out Subscription"
    server.azure_service_bus.event_topic -> server.azure_service_bus.eventsWebhookSub  "Fan-out Subscription"
    server.azure_service_bus.event_topic -> server.azure_service_bus.eventsWriteSub  "Fan-out Subscription"
    server.azure_service_bus.eventsWriteSub -> server.events_processor.event_repository_handler "EventListenerService"
    server.events_processor.event_repository_handler -> server.database "Permanent storage"
    server.azure_service_bus.eventsSlackSub -> server.events_processor.event_integration_handler "EventListenerService"
    server.azure_service_bus.eventsHecSub -> server.events_processor.event_integration_handler "EventListenerService"
    server.azure_service_bus.eventsWebhookSub -> server.events_processor.event_integration_handler "EventListenerService"
    server.events_processor.event_integration_handler -> server.azure_service_bus.integration_topic "Publishes To"
    server.events_processor.event_integration_handler -> server.events_processor.integration_configuration_details_cache_service "Fetches configurations from"
    server.events_processor.integration_configuration_details_cache_service -> server.database "Fetches configurations from"
    server.events_processor.event_integration_handler -> server.events_processor.integration_filter_service "Runs filters"
    server.azure_service_bus.integration_topic -> server.azure_service_bus.integrationSlackSub "Subscription filtered via Slack key"
    server.azure_service_bus.integration_topic -> server.azure_service_bus.integrationHecSub "Subscription filtered via HEC key"
    server.azure_service_bus.integration_topic -> server.azure_service_bus.integrationWebhookSub "Subscription filtered via Webhook key"
    server.azure_service_bus.integrationSlackSub -> server.events_processor.slack_integration_handler "IntegrationListenerService"
    server.azure_service_bus.integrationWebhookSub -> server.events_processor.webhook_integration_handler "IntegrationListenerService"
    server.azure_service_bus.integrationHecSub -> server.events_processor.webhook_integration_handler "IntegrationListenerService"
    server.events_processor.slack_integration_handler -> server.events_processor.slack_service "Uses"
    server.events_processor.slack_service  -> external_services.slack "Publishes configured events to"
    server.events_processor.webhook_integration_handler -> server.events_processor.http_client "Uses"
    server.events_processor.http_client -> external_services.crowdstrike "Publishes configured events to"
    server.events_processor.http_client -> external_services.datadog "Publishes configured events to"
    server.events_processor.http_client -> external_services.splunk "Publishes configured events to"
}
