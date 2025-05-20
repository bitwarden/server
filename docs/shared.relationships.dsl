# User Relationships
user -> clients.web "Uses"
user -> clients.ios "Uses"
user -> clients.android "Uses"
user -> clients.browser_extension "Uses"
user -> clients.cli "Uses"
user -> clients.desktop "Uses"
admin -> clients.web "Administers Organizations"
provider -> server.portal "Completes Provider registration with"
provider -> clients.web "Administers Providers and Organizations"
customer_success -> server.portal "Inspects and supports"
system_admin -> server.portal "Administers System"

# High-level Client Relationships
clients.web -> server.api "Makes requests to"
clients.ios -> server.api "Makes requests to"
clients.android -> server.api "Makes requests to"
clients.browser_extension -> server.api "Makes requests to"
clients.cli -> server.api "Makes requests to"
clients.desktop -> server.api "Makes requests to"
clients.web -> server.identity "Authenticates with"
clients.ios -> server.identity "Authenticates With"
clients.android -> server.identity "Authenticates With"
clients.browser_extension -> server.identity "Authenticates With"
clients.cli -> server.identity "Authenticates With"
clients.desktop -> server.identity "Authenticates With"
server.api -> server.identity "Validates JWTs with" {
  url "https://bitwarden.com"
}
clients -> server.events "Posts local usage events to"

# Database Relationships

server.api -> server.database "Queries"
server.portal -> server.database "Queries"

# queue Relationships
server.api -> server.events_queue "Sends events to"
server.events -> server.events_queue "Sends events to"
server.api -> server.mail_queue "Sends emails to"
server.api -> server.notifications_queue "Sends notifications to"
server.notifications -> server.notifications_queue "Sends notifications to"
server.events_queue -> server.events_processor "Processes events from"
server.mail_queue -> server.portal "Processes emails from"

# self host phone home
self_hosted_instances -> server.notifications "Sends push notification proxy requests to"
