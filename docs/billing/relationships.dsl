# High-level provider relationships
server.api.billing -> stripe "Requests payments for customers"
server.api.billing -> braintree "Requests payments for customers"
stripe -> server.api.billing "Sends subscription events to"
