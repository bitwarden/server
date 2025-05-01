workspace "Bitwarden" "General Bitwarden System" {

    !identifiers hierarchical

    model {
        !include "common.models.dsl"
        payment_systems = softwareSystem "Payment Systems" {
            tags "External"
        }

        bitwarden_pm = softwareSystem "Bitwarden System" {
            wa = container "Web Application"
            db = container "Database Schema" {
                tags "Database"
            }
        }

        identity = softwareSystem "Identity" {
            tags "Auth"
            # This would point to a production on-prem instance hosting an auth-owned workspace defining an Identity system
            url "http://localhost:8085/workspace/3/diagrams#Identity"
        }

        user -> bitwarden_pm "Uses"
        user -> identity "Authenticates with"
        bitwarden_pm -> identity "validates tokens with"
        admin -> bitwarden_pm "Administers Organizations"
        provider -> bitwarden_pm "Administers Providers and Organizations"
        customer_success -> bitwarden_pm "Inspects and supports"
        system_admin -> bitwarden_pm "Administers System"
        bitwarden_pm.wa -> bitwarden_pm.db "Reads from and writes to"
    }

    views {
        !include "common.views.dsl"
        systemContext bitwarden_pm "Diagram1" {
            include *
        }

        container bitwarden_pm "Diagram2" {
            include *
        }

        styles {
            element "Element" {
                color #ffffff
            }
            element "Software System" {
                background #f86628
            }
            element "Container" {
                background #f88728
            }
            element "Database" {
                shape cylinder
            }
        }
    }

    configuration {
        scope softwaresystem
    }

}
