!element server {
  icons = container "Icons" {
    icons_controller = component "IconsController" {
      description "IconsController"
      technology "C# ASP.NET Core"
      
    }
    info_controller = component "InfoController" {
      description "Provides information about the deployed icon service. Allow for health checks."
      technology "C# ASP.NET Core"
      tags "Info" "HealthCheck"
    }
    icon_retrieval = component "IconDetermination" {
      description "Resolves a single source for a website icon and downloads it."
      perspectives {
        "Security" "Internal network exposure" 5
      }
    }
    icon_cache = component "IconCache" {
      description "Caches icons for a given domain"
      tags "Cache"
      technology "C# MemoryCache"
    }

    clients -> icons_controller "Requests icons for cleartext urls from"
    icons_controller -> icon_retrieval "Requests icons from"
    icons_controller -> icon_cache "Caches icons in"
  }
}

external_websites = softwareSystem "External Websites" {
  tags "External"
  tags "Icons"
}

server.icons.icon_retrieval -> external_websites "Retrieves icons from"
