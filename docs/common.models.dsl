user = person "User" "An end user of the application"
admin = person "Organization Admin" "An administrator of an organization" {
  tags "Admin"
}
provider = person "MSP" "And employee of a managed service provider" {
  tags "MSP"
}
customer_success = person "Customer Success" "A customer success engineer. Inspects bitwarden state through the admin portal and internal tools" {
  tags "Bitwarden Employee"
}
system_admin = person "System Admin" "Either a Bitwarden site-reliability engineer or administrator of a self-hosted instance" {
  tags "Bitwarden Employee" "Self-Host Admin"
}
api = softwareSystem "API" {
}
