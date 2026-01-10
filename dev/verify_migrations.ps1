#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validates that new database migration files follow naming conventions and chronological order.

.DESCRIPTION
    This script validates migration files to ensure:

    For SQL migrations in util/Migrator/DbScripts/:
    1. New migrations follow the naming format: YYYY-MM-DD_NN_Description.sql
    2. New migrations are chronologically ordered (filename sorts after existing migrations)
    3. Dates use leading zeros (e.g., 2025-01-05, not 2025-1-5)
    4. A 2-digit sequence number is included (e.g., _00, _01)

    For Entity Framework migrations in util/MySqlMigrations, util/PostgresMigrations, util/SqliteMigrations:
    1. New migrations follow the naming format: YYYYMMDDHHMMSS_Description.cs
    2. Each migration has both .cs and .Designer.cs files
    3. New migrations are chronologically ordered (timestamp sorts after existing migrations)

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
    $baseMigrations = git ls-tree -r --name-only $BaseRef -- "$migrationPath/" 2>$null | Where-Object { $_ -like "*.sql" } | Sort-Object
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
$currentMigrations = git ls-tree -r --name-only $CurrentRef -- "$migrationPath/" | Where-Object { $_ -like "*.sql" } | Sort-Object

# Find added migrations
$addedMigrations = $currentMigrations | Where-Object { $_ -notin $baseMigrations }

$sqlValidationFailed = $false

if ($addedMigrations.Count -eq 0) {
    Write-Host "No new SQL migration files added."
    Write-Host ""
}
else {
    Write-Host "New SQL migration files detected:"
    $addedMigrations | ForEach-Object { Write-Host "  $_" }
    Write-Host ""

    # Get the last migration from base reference
    if ($baseMigrations.Count -eq 0) {
        Write-Host "No previous SQL migrations found (initial commit?). Skipping chronological validation."
        Write-Host ""
    }
    else {
        $lastBaseMigration = Split-Path -Leaf ($baseMigrations | Select-Object -Last 1)
        Write-Host "Last SQL migration in base reference: $lastBaseMigration"
        Write-Host ""

        # Required format regex: YYYY-MM-DD_NN_Description.sql
        $formatRegex = '^[0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{2}_.+\.sql$'

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
                $sqlValidationFailed = $true
                continue
            }

            # Compare migration name with last base migration (using ordinal string comparison)
            if ([string]::CompareOrdinal($migrationName, $lastBaseMigration) -lt 0) {
                Write-Host "ERROR: New migration '$migrationName' is not chronologically after '$lastBaseMigration'"
                $sqlValidationFailed = $true
            }
            else {
                Write-Host "OK: '$migrationName' is chronologically after '$lastBaseMigration'"
            }
        }

        Write-Host ""
    }

    if ($sqlValidationFailed) {
        Write-Host "FAILED: One or more SQL migrations are incorrectly named or not in chronological order"
        Write-Host ""
        Write-Host "All new SQL migration files must:"
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
    }
    else {
        Write-Host "SUCCESS: All new SQL migrations are correctly named and in chronological order"
    }

    Write-Host ""
}

# ===========================================================================================
# Validate Entity Framework Migrations
# ===========================================================================================

Write-Host "==================================================================="
Write-Host "Validating Entity Framework Migrations"
Write-Host "==================================================================="
Write-Host ""

$efMigrationPaths = @(
    @{ Path = "util/MySqlMigrations/Migrations"; Name = "MySQL" },
    @{ Path = "util/PostgresMigrations/Migrations"; Name = "Postgres" },
    @{ Path = "util/SqliteMigrations/Migrations"; Name = "SQLite" }
)

$efValidationFailed = $false

foreach ($migrationPathInfo in $efMigrationPaths) {
    $efPath = $migrationPathInfo.Path
    $dbName = $migrationPathInfo.Name

    Write-Host "-------------------------------------------------------------------"
    Write-Host "Checking $dbName EF migrations in $efPath"
    Write-Host "-------------------------------------------------------------------"
    Write-Host ""

    # Get list of migrations from base reference
    try {
        $baseMigrations = git ls-tree -r --name-only $BaseRef -- "$efPath/" 2>$null | Where-Object { $_ -like "*.cs" -and $_ -notlike "*DatabaseContextModelSnapshot.cs" } | Sort-Object
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Warning: Could not retrieve $dbName migrations from base reference '$BaseRef'"
            $baseMigrations = @()
        }
    }
    catch {
        Write-Host "Warning: Could not retrieve $dbName migrations from base reference '$BaseRef'"
        $baseMigrations = @()
    }

    # Get list of migrations from current reference
    $currentMigrations = git ls-tree -r --name-only $CurrentRef -- "$efPath/" | Where-Object { $_ -like "*.cs" -and $_ -notlike "*DatabaseContextModelSnapshot.cs" } | Sort-Object

    # Find added migrations
    $addedMigrations = $currentMigrations | Where-Object { $_ -notin $baseMigrations }

    if ($addedMigrations.Count -eq 0) {
        Write-Host "No new $dbName EF migration files added."
        Write-Host ""
        continue
    }

    Write-Host "New $dbName EF migration files detected:"
    $addedMigrations | ForEach-Object { Write-Host "  $_" }
    Write-Host ""

    # Get the last migration from base reference
    if ($baseMigrations.Count -eq 0) {
        Write-Host "No previous $dbName migrations found. Skipping chronological validation."
        Write-Host ""
    }
    else {
        $lastBaseMigration = Split-Path -Leaf ($baseMigrations | Select-Object -Last 1)
        Write-Host "Last $dbName migration in base reference: $lastBaseMigration"
        Write-Host ""
    }

    # Required format regex: YYYYMMDDHHMMSS_Description.cs or YYYYMMDDHHMMSS_Description.Designer.cs
    $efFormatRegex = '^[0-9]{14}_.+\.cs$'

    # Group migrations by base name (without .Designer.cs suffix)
    $migrationGroups = @{}
    foreach ($migration in $addedMigrations) {
        $migrationName = Split-Path -Leaf $migration

        # Extract base name (remove .Designer.cs or .cs)
        if ($migrationName -match '^([0-9]{14}_.+?)(?:\.Designer)?\.cs$') {
            $baseName = $matches[1]
            if (-not $migrationGroups.ContainsKey($baseName)) {
                $migrationGroups[$baseName] = @()
            }
            $migrationGroups[$baseName] += $migrationName
        }
    }

    foreach ($baseName in $migrationGroups.Keys | Sort-Object) {
        $files = $migrationGroups[$baseName]

        # Validate format
        $mainFile = "$baseName.cs"
        $designerFile = "$baseName.Designer.cs"

        if ($mainFile -notmatch $efFormatRegex) {
            Write-Host "ERROR: Migration '$mainFile' does not match required format"
            Write-Host "Required format: YYYYMMDDHHMMSS_Description.cs"
            Write-Host "  - YYYYMMDDHHMMSS: 14-digit timestamp (Year, Month, Day, Hour, Minute, Second)"
            Write-Host "Example: 20250115120000_AddNewFeature.cs"
            $efValidationFailed = $true
            continue
        }

        # Check that both .cs and .Designer.cs files exist
        $hasCsFile = $files -contains $mainFile
        $hasDesignerFile = $files -contains $designerFile

        if (-not $hasCsFile) {
            Write-Host "ERROR: Missing main migration file: $mainFile"
            $efValidationFailed = $true
        }

        if (-not $hasDesignerFile) {
            Write-Host "ERROR: Missing designer file: $designerFile"
            Write-Host "Each EF migration must have both a .cs and .Designer.cs file"
            $efValidationFailed = $true
        }

        if (-not $hasCsFile -or -not $hasDesignerFile) {
            continue
        }

        # Compare migration timestamp with last base migration (using ordinal string comparison)
        if ($baseMigrations.Count -gt 0) {
            if ([string]::CompareOrdinal($mainFile, $lastBaseMigration) -lt 0) {
                Write-Host "ERROR: New migration '$mainFile' is not chronologically after '$lastBaseMigration'"
                $efValidationFailed = $true
            }
            else {
                Write-Host "OK: '$mainFile' is chronologically after '$lastBaseMigration'"
            }
        }
        else {
            Write-Host "OK: '$mainFile' (no previous migrations to compare)"
        }
    }

    Write-Host ""
}

if ($efValidationFailed) {
    Write-Host "FAILED: One or more EF migrations are incorrectly named or not in chronological order"
    Write-Host ""
    Write-Host "All new EF migration files must:"
    Write-Host "  1. Follow the naming format: YYYYMMDDHHMMSS_Description.cs"
    Write-Host "  2. Include both .cs and .Designer.cs files"
    Write-Host "  3. Have a timestamp that sorts after the last migration in base"
    Write-Host ""
    Write-Host "To fix this issue:"
    Write-Host "  1. Locate your migration file(s) in the respective Migrations directory"
    Write-Host "  2. Ensure both .cs and .Designer.cs files exist"
    Write-Host "  3. Rename to follow format: YYYYMMDDHHMMSS_Description.cs"
    Write-Host "  4. Ensure the timestamp is after the last migration"
    Write-Host ""
    Write-Host "Example: 20250115120000_AddNewFeature.cs and 20250115120000_AddNewFeature.Designer.cs"
}
else {
    Write-Host "SUCCESS: All new EF migrations are correctly named and in chronological order"
}

Write-Host ""
Write-Host "==================================================================="
Write-Host "Validation Summary"
Write-Host "==================================================================="

if ($sqlValidationFailed -or $efValidationFailed) {
    if ($sqlValidationFailed) {
        Write-Host "❌ SQL migrations validation FAILED"
    }
    else {
        Write-Host "✓ SQL migrations validation PASSED"
    }

    if ($efValidationFailed) {
        Write-Host "❌ EF migrations validation FAILED"
    }
    else {
        Write-Host "✓ EF migrations validation PASSED"
    }

    Write-Host ""
    Write-Host "OVERALL RESULT: FAILED"
    exit 1
}
else {
    Write-Host "✓ SQL migrations validation PASSED"
    Write-Host "✓ EF migrations validation PASSED"
    Write-Host ""
    Write-Host "OVERALL RESULT: SUCCESS"
    exit 0
}
