admin = person "Organization Admin" "An administrator of an organization" {
  tags "Admin"
}
provider = person "MSP" "And employee of a managed service provider" {
  tags "MSP"
}

!element server {
  scim = container "SCIM" {
    tags "SCIM"
  }
}

directory_connector -> server.api "Syncs users and groups to Bitwarden"
