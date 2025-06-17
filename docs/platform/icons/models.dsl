!element server {
  icons = container "Icons" {
    !docs "threat_model.md"
    icons_controller = component "IconsController" {
      description "IconsController"
      technology "C# ASP.NET Core"
      
    }
    info_controller = component "InfoController" {
      description "Provides information about the deployed icon service. Allow for health checks."
      technology "C# ASP.NET Core"
      tags "Info" "HealthCheck"
    }
    icon_determination = component "IconDetermination" {
      description "Resolves a single source for a website icon and downloads it."
    }
    icon_cache = component "IconCache" {
      description "Caches icons for a given domain"
      tags "Cache"
      technology "C# MemoryCache"
    }

    clients -> icons_controller "Requests icons for cleartext urls from" {
      perspectives {
        "Security" "\
        Icons 1.2.1 Broken SSL communication exposes vault contents to network administrators \n\n\
        Icons 1.2.2 Tracking of user vault contents by ip correlation between identity and icons services \n\n\
        Icons 1.2.3 No SLA offered on Icons service, graceful degradation of features needed if it goes down \n\n\
        Icons 1.2.4 SSRF through crafted input resolving to a location the server has elevated privileges in\
        "
      }
    }
    icons_controller -> icon_determination "Requests icons from"
    icons_controller -> icon_cache "Caches icons in" {
      perspectives {
        "Security" "\
        Icons 1.3.1 Aggregate vault content leak through timing attack on cache \n\n\
        Icons 1.3.2 Possible injection attack through cache key \n\n\
        Icons 1.3.3 & Icons 1.3.4 Cache bloat leading to DoS \n\n\
        Icons 1.3.5 Cache poisoning leading to incorrect icon storage \
        "
      }
    }
  }
}

dns = softwareSystem "DNS" {
  tags "External"
  tags "Icons"
}

server.icons.icon_determination -> dns "Resolves IP addresses for domain names from"

external_websites = softwareSystem "External Websites" {
  tags "External"
  tags "Icons"
}

server.icons.icon_determination -> external_websites "Retrieves icons from"
