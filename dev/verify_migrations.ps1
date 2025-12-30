#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validates that new database migration files follow naming conventions and chronological order.

.DESCRIPTION
    This script validates migration files in util/Migrator/DbScripts/ to ensure:
    1. New migrations follow the naming format: YYYY-MM-DD_NN_Description.sql
    2. New migrations are chronologically ordered (filename sorts after existing migrations)
    3. Dates use leading zeros (e.g., 2025-01-05, not 2025-1-5)
    4. A 2-digit sequence number is included (e.g., _00, _01)

.PARAMETER BaseRef
    The base git reference to compare against (e.g., 'main', 'HEAD~1')

.PARAMETER CurrentRef
    The current git reference (defaults to 'HEAD')

.EXAMPLE
    # For pull requests - compare against main branch
    .\verify_migrations.ps1 -BaseRef main

.EXAMPLE
    # For pushes - compare against previous commit
    .\verify_migrations.ps1 -BaseRef HEAD~1
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$BaseRef,

    [Parameter(Mandatory = $false)]
    [string]$CurrentRef = "HEAD"
)

# Use invariant culture for consistent string comparison
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

$migrationPath = "util/Migrator/DbScripts"

# Get list of migrations from base reference
try {
    $baseMigrations = git ls-tree -r --name-only $BaseRef -- "$migrationPath/*.sql" 2>$null | Sort-Object
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Warning: Could not retrieve migrations from base reference '$BaseRef'"
        $baseMigrations = @()
    }
}
catch {
    Write-Host "Warning: Could not retrieve migrations from base reference '$BaseRef'"
    $baseMigrations = @()
}

# Get list of migrations from current reference
$currentMigrations = git ls-tree -r --name-only $CurrentRef -- "$migrationPath/*.sql" | Sort-Object

# Find added migrations
$addedMigrations = $currentMigrations | Where-Object { $_ -notin $baseMigrations }

if ($addedMigrations.Count -eq 0) {
    Write-Host "No new migration files added."
    exit 0
}

Write-Host "New migration files detected:"
$addedMigrations | ForEach-Object { Write-Host "  $_" }
Write-Host ""

# Get the last migration from base reference
if ($baseMigrations.Count -eq 0) {
    Write-Host "No previous migrations found (initial commit?). Skipping validation."
    exit 0
}

$lastBaseMigration = Split-Path -Leaf ($baseMigrations | Select-Object -Last 1)
Write-Host "Last migration in base reference: $lastBaseMigration"
Write-Host ""

# Required format regex: YYYY-MM-DD_NN_Description.sql
$formatRegex = '^[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}_.+\.sql$'

$validationFailed = $false

foreach ($migration in $addedMigrations) {
    $migrationName = Split-Path -Leaf $migration

    # Validate NEW migration filename format
    if ($migrationName -notmatch $formatRegex) {
        Write-Host "ERROR: Migration '$migrationName' does not match required format"
        Write-Host "Required format: YYYY-MM-DD_NN_Description.sql"
        Write-Host "  - YYYY: 4-digit year"
        Write-Host "  - MM: 2-digit month with leading zero (01-12)"
        Write-Host "  - DD: 2-digit day with leading zero (01-31)"
        Write-Host "  - NN: 2-digit sequence number (00, 01, 02, etc.)"
        Write-Host "Example: 2025-01-15_00_MyMigration.sql"
        $validationFailed = $true
        continue
    }

    # Compare migration name with last base migration (using ordinal string comparison)
    if ([string]::CompareOrdinal($migrationName, $lastBaseMigration) -lt 0) {
        Write-Host "ERROR: New migration '$migrationName' is not chronologically after '$lastBaseMigration'"
        $validationFailed = $true
    }
    else {
        Write-Host "OK: '$migrationName' is chronologically after '$lastBaseMigration'"
    }
}

Write-Host ""

if ($validationFailed) {
    Write-Host "FAILED: One or more migrations are incorrectly named or not in chronological order"
    Write-Host ""
    Write-Host "All new migration files must:"
    Write-Host "  1. Follow the naming format: YYYY-MM-DD_NN_Description.sql"
    Write-Host "  2. Use leading zeros in dates (e.g., 2025-01-05, not 2025-1-5)"
    Write-Host "  3. Include a 2-digit sequence number (e.g., _00, _01)"
    Write-Host "  4. Have a filename that sorts after the last migration in base"
    Write-Host ""
    Write-Host "To fix this issue:"
    Write-Host "  1. Locate your migration file(s) in util/Migrator/DbScripts/"
    Write-Host "  2. Rename to follow format: YYYY-MM-DD_NN_Description.sql"
    Write-Host "  3. Ensure the date is after $lastBaseMigration"
    Write-Host ""
    Write-Host "Example: 2025-01-15_00_AddNewFeature.sql"
    exit 1
}

Write-Host "SUCCESS: All new migrations are correctly named and in chronological order"
exit 0
