!element server {
    azure_service_bus = container "Azure Service Bus" {
        description "AMQP service used for pub/sub architecture for Events and Integrations"
        tags "Events", "Azure", "ASB"

        event_topic = component "Event Topic" {
            description "The main entry point for all events in the system. When an event occurs, it is published to this topic."
            tags "Events", "ASB", "Event Tier"
        }

        integration_topic = component "Integration Topic" {
            description "Events that have integrations configured are processed and put on the integration topic with a routing key for their specific integration handler to process."
            tags "Events", "ASB", "Integrations", "Integration Tier"
        }

        eventsWriteSub = component "events-write-subscription" {
            description "Subscription for EventRepositoryHandler to write all events into azure table storage."
            tags "ASB", "Subscription", "Event Tier"
        }

        eventsSlackSub = component "events-slack-subscription" {
            description "Subscription for slack-specific EventIntegrationHandler which publishes processed events to the integration tier if there is a Slack integration configured."
            tags "ASB", "Subscription", "Event Tier", "Slack"
        }

        eventsWebhookSub = component "events-webhook-subscription" {
            description "Subscription for webhook-specific EventIntegrationHandler which publishes processed events to the integration tier if there is a webhook integration configured."
            tags "ASB", "Subscription", "Event Tier", "Webhook"
        }

        eventsHecSub = component "events-hec-subscription" {
            description "Subscription for HEC-specific EventIntegrationHandler which publishes processed events to the integration tier if there is a HEC integration configured."
            tags "ASB", "Subscription", "Event Tier", "HEC"
        }

        integrationSlackSub = component "integration-slack-subscription" {
            description "Integration-level subscription for Slack IntegrationMessages. Correlation filter: Label = 'slack'."
            tags "ASB", "Subscription", "Integration Tier", "Slack"
        }

        integrationWebhookSub = component "integration-webhook-subscription" {
            description "Integration-level subscription for Webhook IntegrationMessages. Correlation filter: Label = 'webhook'."
            tags "ASB", "Subscription", "Integration Tier", "Webhook"
        }

        integrationHecSub = component "integration-hec-subscription" {
            description "Integration-level subscription for HEC IntegrationMessages. Correlation filter: Label = 'hec'."
            tags "ASB", "Subscription", "Integration Tier", "HEC"
        }
    }

    rabbit_mq = container "RabbitMQ" {
        tags "Events"
        tags "RabbitMQ"

        event_exchange = component "Event Exchange" {
            tags "Events", "Event Tier"
        }

        integration_exchange = component "Integration Exchange" {
            tags "Events", "Integrations", "Integration Tier"
        }

        eventsWriteQueue = component "events-write-queue" {
            description "Queue for EventRepositoryHandler to write all events into the database."
            tags "RabbitMQ", "Queue", "Event Tier"
        }

        eventsSlackQueue = component "events-slack-queue" {
            description "Queue for slack-specific EventIntegrationHandler which publishes processed events to the integration tier if there is a Slack integration configured."
            tags "RabbitMQ", "Queue", "Event Tier", "Slack"
        }

        eventsWebhookQueue = component "events-webhook-queue" {
            description "Queue for webhook-specific EventIntegrationHandler which publishes processed events to the integration tier if there is a webhook integration configured."
            tags "RabbitMQ", "Queue", "Event Tier", "Webhook"
        }

        eventsHecQueue = component "events-hec-queue" {
            description "Queue for HEC-specific EventIntegrationHandler which publishes processed events to the integration tier if there is a HEC integration configured."
            tags "RabbitMQ", "Queue", "Event Tier", "HEC"
        }

        integrationSlackQueue = component "integration-slack-queue" {
            description "Integration-level queue for Slack IntegrationMessages. Routing key = 'slack'."
            tags "RabbitMQ", "Queue", "Integration Tier", "Slack"
        }

        integrationWebhookQueue = component "integration-webhook-queue" {
            description "Integration-level queue for Webhook IntegrationMessages. Routing key = 'webhook'."
            tags "RabbitMQ", "Queue", "Integration Tier", "Webhook"
        }

        integrationHecQueue = component "integration-hec-queue" {
            description "Integration-level queue for HEC IntegrationMessages. Routing key = 'hec'."
            tags "RabbitMQ", "Queue", "Integration Tier", "HEC"
        }

        integrationSlackRetryQueue = component "integration-slack-retry-queue" {
            description "Integration-level retry queue for Slack IntegrationMessages. Routing key Label = 'slack-retry'."
            tags "RabbitMQ", "Queue", "Integration Tier", "Slack"
        }

        integrationWebhookRetryQueue = component "integration-webhook-retry-queue" {
            description "Integration-level retry queue for Webhook IntegrationMessages. Routing key = 'webhook-retry'."
            tags "RabbitMQ", "Queue", "Integration Tier", "Webhook"
        }

        integrationHecRetryQueue = component "integration-hec-retry-queue" {
            description "Integration-level retry queue for HEC IntegrationMessages. Routing key = 'hec-retry'."
            tags "RabbitMQ", "Queue", "Integration Tier", "HEC"
        }
    }
}

!element server.events_processor {
    !docs "architecture.md"

    event_repository_handler = component "EventRepositoryHandler" {
        description "Handles all events, passing them off to the IEventWriteService with the `persistent` key for long term storage."
    }
    event_integration_handler = component "EventIntegrationHandler" {
        description "Fetches the relevent configurations when an event comes in and hands the event to its paired integration handler for processing."
    }
    slack_integration_handler = component "SlackIntegrationHandler" {
        description "Processes Slack IntegrationMessages, posting them to the configured channels."
    }
    webhook_integration_handler = component "WebhookIntegrationHandler" {
        description "Processes Webhook and HEC IntegrationMessages, posting them to the configured URI."
    }
    integration_configuration_details_cache_service = component "IntegrationConfigurationDetailsCacheService" {
        description "Caches all configurations for integrations in memory so that events can be handled without adding database load."
    }
    slack_service = component "SlackService" {
        description "Handles all API interaction with Slack."
    }
    http_client = component "HttpClient" {
        description "Performs any Http functions for Webhooks / HEC."
    }
    integration_filter_service = component "IntegrationFilterService" {
        description "Processes filters from configurations to determine if an event should be processed out to the integration."
    }
}

!element server.events {
    event_repository_handler = component "EventRepositoryHandler" {
        description "Handles all events, passing them off to the IEventWriteService with the `persistent` key for long term storage."
    }
    event_integration_handler = component "EventIntegrationHandler" {
        description "Fetches the relevent configurations when an event comes in and hands the event to its paired integration handler for processing."
    }
    slack_integration_handler = component "SlackIntegrationHandler" {
        description "Processes Slack IntegrationMessages, posting them to the configured channels."
    }
    webhook_integration_handler = component "WebhookIntegrationHandler" {
        description "Processes Webhook and HEC IntegrationMessages, posting them to the configured URI."
    }
    integration_configuration_details_cache_service = component "IntegrationConfigurationDetailsCacheService" {
        description "Caches all configurations for integrations in memory so that events can be handled without adding database load."
    }
    slack_service = component "SlackService" {
        description "Handles all API interaction with Slack."
    }
    http_client = component "HttpClient" {
        description "Performs any Http functions for Webhooks / HEC."
    }
    integration_filter_service = component "IntegrationFilterService" {
        description "Processes filters from configurations to determine if an event should be processed out to the integration."
    }
}

external_services = softwareSystem "External Services" {
    tags "External", "Events", "Integrations"
    description "External services (e.g. SIEM, Slack, et al) that consume events via integrations"

    slack = container "Slack" {
        tags "External", "Events", "Integrations", "Slack"
        description "Slack messaging service. Receives messages via configured event integrations."
    }

    splunk = container "Splunk" {
        tags "External", "Events", "Integrations", "Splunk"
        description "Splunk SIEM service. Receives events via configured event integrations."
    }

    datadog = container "Datadog" {
        tags "External", "Events", "Integrations", "Datadog"
        description "Datadog SIEM service. Receives events via configured event integrations."
    }

    crowdstrike = container "Crowdstrike Falcon" {
        tags "External", "Events", "Integrations", "Crowdstrike Falcon", "Crowdstrike"
        description "Crowdstrike Falcon SIEM service. Receives events via configured event integrations."
    }
}
