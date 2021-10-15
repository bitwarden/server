#!/usr/bin/env pwsh
# Script for configuring the initial state of Azurite Storage account
#  Can be run multiple times without negative impact

# Start configuration
$corsRules = (@{
        AllowedHeaders  = @("*");
        ExposedHeaders  = @("*");
        AllowedOrigins  = @("*");
        MaxAgeInSeconds = 30;
        AllowedMethods  = @("Get", "PUT");
    });
$containers = "attachments", "sendfiles", "misc";
$queues = "event", "notifications", "reference-events", "mail";
$tables = "event", "metadata", "installationdevice";
# End configuration

$context = New-AzStorageContext -Local

foreach ($container in $containers) {
    if (Get-AzStorageContainer -Name $container -Context $context -ErrorAction SilentlyContinue) {
        Write-Host -ForegroundColor Magenta "Container already exists:" $container
    }
    else {
        New-AzStorageContainer -Name $container -Context $context
    }
}

foreach ($queue in $queues) {
    if (Get-AzStorageQueue -Name $queue -Context $context -ErrorAction SilentlyContinue) {
        Write-Host -ForegroundColor Magenta "Queue already exists:" $queue
    }
    else {
        New-AzStorageQueue -Name $queue -Context $context
    }
}

foreach ($table in $tables) {
    if (Get-AzStorageTable -Name $table -Context $context -ErrorAction SilentlyContinue) {
        Write-Host -ForegroundColor Magenta "Table already exists:" $table
    }
    else {
        New-AzStorageTable -Name $table -Context $context
    }
}

Set-AzStorageCORSRule -ServiceType Blob -CorsRules $corsRules -Context $context
