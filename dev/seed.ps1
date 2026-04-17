#!/usr/bin/env pwsh
# Runs SeederUtility for each entry defined in seeds.json (and optionally seeds.local.json).
# Usage: ./seed.ps1 [-DryRun]

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$globalSeedsPath = Join-Path $PSScriptRoot "seeds.json"
$localSeedsPath  = Join-Path $PSScriptRoot "seeds.local.json"
$seederProject   = Join-Path $PSScriptRoot ".." "util" "SeederUtility"

$seeds = @(Get-Content $globalSeedsPath -Raw | ConvertFrom-Json)

if (Test-Path $localSeedsPath) {
    $seeds += @(Get-Content $localSeedsPath -Raw | ConvertFrom-Json)
}

function ConvertTo-KebabCase {
    param([string]$Name)
    return ($Name -creplace '(?<=[a-z])([A-Z])', '-$1').ToLower()
}

function Build-CliArgs {
    param($ArgsObject)
    $parts = @()
    foreach ($prop in $ArgsObject.PSObject.Properties) {
        $flag  = ConvertTo-KebabCase $prop.Name
        $value = $prop.Value
        if ($null -eq $value) {
            continue
        } elseif ($value -is [bool]) {
            if ($value) {
                $parts += "--$flag"
            }
        } else {
            $parts += "--$flag"
            $parts += "$value"
        }
    }
    return ,$parts
}

$total = $seeds.Count

if ($total -eq 0) {
    Write-Host "No seeds configured." -ForegroundColor Yellow
    exit 0
}

for ($i = 0; $i -lt $total; $i++) {
    $seed    = $seeds[$i]
    $label   = if ($seed.label) { $seed.label } else { "$($seed.command) #$($i + 1)" }
    $command = $seed.command
    $cliArgs = Build-CliArgs $seed.args

    Write-Host ""
    Write-Host "[$($i + 1)/$total] $label" -ForegroundColor Cyan
    Write-Host "  dotnet run --project $seederProject -- $command $($cliArgs -join ' ')" -ForegroundColor DarkGray

    if (-not $DryRun) {
        dotnet run --project $seederProject -- $command @cliArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Seed '$label' failed with exit code $LASTEXITCODE."
            exit $LASTEXITCODE
        }
    }
}

Write-Host ""
if ($DryRun) {
    Write-Host "Dry run complete. $total seed(s) would be executed." -ForegroundColor Yellow
} else {
    Write-Host "All $total seed(s) completed successfully." -ForegroundColor Green
}
