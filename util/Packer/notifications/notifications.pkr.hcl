### Variables ###
variable "tenant_id" {
  type = string
}

variable "subscription_id" {
  type    = string
}

variable "client_id" {
  type    = string
}

variable "client_secret" {
  type    = string
}

variable "domain" {
  type    = string
  default = "notifications.bitwarden.com"
}

variable "image_resource_group_name" {
  type    = string
  default = "qa-packer-images"
}

variable "commit_hash" {
  type    = string
  default = "000000000000000000000000000000"
}

### Resources ###

locals {
  image_name = "${substr(var.commit_hash, 0, 6)}-notificationsServer-${formatdate("DDMMYYYY", timestamp())}"
}

source "azure-arm" "notifications" {
  client_id = var.client_id
  client_secret = var.client_secret
  subscription_id = var.subscription_id
  tenant_id = var.tenant_id
  azure_tags = {
    dept    = "DevOps"
    task    = "Image deployment"
    service = "notifications"
  }
  image_offer                       = "UbuntuServer"
  image_publisher                   = "Canonical"
  image_sku                         = "18.04-LTS"
  build_resource_group_name        = var.image_resource_group_name
  managed_image_name                = local.image_name
  managed_image_resource_group_name = var.image_resource_group_name
  os_type                           = "Linux"
  vm_size                           = "Standard_DS2_v2"

  #### Uncomment for building locally using the Azure CLI
  # location                          = "East US"
  # use_azure_cli_auth                = true
}


build {

  sources = ["source.azure-arm.notifications"]

  provisioner "shell-local" {
    inline = ["envsubst < nginx/default_template > nginx/default"]
    environment_vars = [
      "domain=${var.domain}"
    ]
  }

  provisioner "file" {
    source      = "ssl/"
    destination = "~/"
  }

  provisioner "file" {
    source      = "bitwarden.env"
    destination = "~/bitwarden.env"
  }

  provisioner "file" {
    source      = "scripts/setup.sh"
    destination = "/tmp/setup.sh"
  }

  provisioner "file" {
    source      = "nginx/"
    destination = "/tmp/"
  }

  provisioner "file" {
    source      = "artifact/Notifications.zip"
    destination = "/tmp/Notifications.zip"
  }

  provisioner "shell" {
    inline = ["chmod +x /tmp/setup.sh", "sudo bash /tmp/setup.sh"]
  }

  #### Post setup tests
  provisioner "shell" {
    inline = [
      "sleep 5",
      "curl 'localhost:5000/alive'"]
  }

}
