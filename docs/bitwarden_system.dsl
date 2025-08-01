workspace "Bitwarden Server System" {

  !identifiers hierarchical

  !docs "usage_docs"
  model {
    properties {
      "structurizr.groupSeparator" "/"
    }

    # Include shared level models
    !include "shared.models.dsl"

    # Include team level models
    !include "admin_console/models.dsl"
    !include "auth/models.dsl"
    !include "billing/models.dsl"
    !include "key_management/models.dsl"
    !include "platform/models.dsl"
    !include "tools/models.dsl"
    !include "vault/models.dsl"

    # Include shared level relationships
    !include "shared.relationships.dsl"

    !include "admin_console/relationships.dsl"
    !include "auth/relationships.dsl"
    !include "billing/relationships.dsl"
    !include "key_management/relationships.dsl"
    !include "platform/relationships.dsl"
    !include "tools/relationships.dsl"
    !include "vault/relationships.dsl"
  }

  views {
    !include "admin_console/views.dsl"
    !include "auth/views.dsl"
    !include "billing/views.dsl"
    !include "key_management/views.dsl"
    !include "platform/views.dsl"
    !include "tools/views.dsl"
    !include "vault/views.dsl"

    systemLandscape "Bitwarden" {
      include *
    }

    container server "Bitwarden_Server" {
      include *
    }

    filtered Bitwarden_Server exclude "Self-Hosted-Only" "Cloud"
    filtered Bitwarden_Server exclude "Cloud-Only" "Self-Hosted"

    // This is last to override team styles with common styles
    !include "shared.views.dsl"
  }
}
