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
    Write-Output "All existing user secrets will be cleared"
}

$projects = "Admin", "Api", "Billing", "Events", "EventsProcessor", "Icons", "Identity", "Notifications"
$bwLicenseProjects = "Sso"
$allProjects = $() + $projects + $bwLicenseProjects

foreach ($project in $allProjects) {
    if ($bwLicenseProjects.Contains($project)) {
        $path = "../bitwarden_license/src/" + $project
    } else {
        $path = "../src/" + $project
    }

    if ($clear -eq $true) {
        dotnet user-secrets clear -p "$path"
    }
    $output = Get-Content secrets.json | & dotnet user-secrets set -p "$path"
    Write-Output "$output - $project"
}
