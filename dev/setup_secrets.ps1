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

$projects = "Admin", "Api", "Billing", "Events", "EventsProcessor", "Icons", "Identity", "Notifications";

foreach ($projects in $projects) {
    if ($clear -eq $true) {
        dotnet user-secrets clear -p "../src/$projects"
    }
    Get-Content secrets.json | & dotnet user-secrets set -p "../src/$projects"
}
