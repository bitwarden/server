component server.azure_service_bus "Azure_Service_Bus" {
    include *
}

component server.rabbit_mq "RabbitMQ" {
    include *
}

component server.events_processor "Events_Processor" {
    include *
}

component server.events "Events" {
    include *
}

dynamic server.events_processor "Events_Processor_Azure_Service_Bus" "Event Integrations / ASB Detail" {
    eventsWriteSub
    eventsHecSub
    eventsSlackSub
    eventsWebhookSub
    eventsWriteListener
    eventsHecListener
    eventsSlackListener
    eventsWebhookListener
    eventsWriteDelegate
    eventRepositoryDatabase
    eventsIntegrationHandlerDelegate
    eventIntegrationHandlerCache
    cacheDatabaseFetch
    eventIntegrationHandlerFilter
    eventIntegrationHandlerPublish
    integrationSlackSub
    integrationWebhookSub
    integrationHecSub
    integrationSlackListener
    integrationWebhookListener
    integrationHecListener
    integrationSlackDelegate
    integrationWebhookDelegate
    slackToSlackService
    slackServiceToSlack
    handlerHttpClient
    httpToCrowdstrike
    httpToDatadog
    httpToSplunk
}

dynamic server.events "Events_RabbitMQ" "Event Integrations / RabbitMQ Detail" {
    eventsWriteQueue
    eventsHecQueue
    eventsSlackQueue
    eventsWebhookQueue
    eventsWriteListener_events
    eventsHecListener_events
    eventsSlackListener_events
    eventsWebhookListener_events
    eventsWriteDelegate_events
    eventRepositoryDatabase_events
    eventsIntegrationHandlerDelegate_events
    eventIntegrationHandlerCache_events
    cacheDatabaseFetch_events
    eventIntegrationHandlerFilter_events
    eventIntegrationHandlerPublish_events
    integrationSlackQueue
    integrationWebhookQueue
    integrationHecQueue
    integrationSlackListener_events
    integrationWebhookListener_events
    integrationHecListener_events
    integrationSlackDelegate_events
    integrationWebhookDelegate_events
    slackToSlackService_events
    slackServiceToSlack_events
    handlerHttpClient_events
    httpToCrowdstrike_events
    httpToDatadog_events
    httpToSplunk_events
}
