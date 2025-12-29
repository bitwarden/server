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
    eventIntegrationHandlerDatabase
    eventIntegrationHandlerCache
    cacheDatabaseFetch
    eventIntegrationHandlerFilter
    eventIntegrationHandlerPublish
    integrationSlackSub
    integrationTeamsSub
    integrationDatadogSub
    integrationWebhookSub
    integrationHecSub
    integrationSlackListener
    integrationTeamsListener
    integrationDatadogListener
    integrationWebhookListener
    integrationHecListener
    integrationSlackDelegate
    integrationTeamsDelegate
    integrationDatadogDelegate
    integrationWebhookDelegate
    slackToSlackService
    slackServiceToSlack
    teamsToTeamsService
    teamsServiceToTeams
    datadogHandlerHttpClient
    webhookHandlerHttpClient
    httpToDatadog
    httpToCrowdstrike
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
    eventIntegrationHandlerDatabase_events
    eventIntegrationHandlerCache_events
    cacheDatabaseFetch_events
    eventIntegrationHandlerFilter_events
    eventIntegrationHandlerPublish_events
    integrationSlackQueue
    integrationWebhookQueue
    integrationHecQueue
    integrationTeamsQueue
    integrationDatadogQueue
    integrationSlackListener_events
    integrationTeamsListener_events
    integrationDatadogListener_events
    integrationWebhookListener_events
    integrationHecListener_events
    integrationSlackDelegate_events
    integrationTeamsDelegate_events
    integrationDatadogDelegate_events
    integrationWebhookDelegate_events
    slackToSlackService_events
    slackServiceToSlack_events
    teamsToTeamsService_events
    teamsServiceToTeams_events
    webhookHandlerHttpClient_events
    datadogHandlerHttpClient_events
    httpToDatadog_events
    httpToCrowdstrike_events
    httpToSplunk_events
}
