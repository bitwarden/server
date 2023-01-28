#!/usr/bin/env pwsh
# Helper script for applying the same user secrets to each project
param (
    [bool]$clear,
    [Parameter(ValueFromRemainingArguments = $true, Position=1)]
    $cmdArgs
)

if (!(Test-Path "secrets.json")) {
    Write-Warning "No secrets.json file found, please copy and modify the provided example";
    exit;
}

if ($clear -eq $true) {
    Write-Output "Deleting all existing user secrets"
}

$projects = @{
    Admin = "../src/Admin"
    Api = "../src/Api"
    Billing = "../src/Billing"
    Events = "../src/Events"
    EventsProcessor = "../src/EventsProcessor"
    Icons = "../src/Icons"
    Identity = "../src/Identity"
    Notifications = "../src/Notifications"
    Sso = "../bitwarden_license/src/Sso" 
    Scim = "../bitwarden_license/src/Scim" 
}

foreach ($key in $projects.keys) {
    if ($clear -eq $true) {
        dotnet user-secrets clear -p $projects[$key]
    }
    $output = Get-Content secrets.json | & dotnet user-secrets set -p $projects[$key]
    Write-Output "$output - $key"
}
