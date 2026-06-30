#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Logging helper functions for Bitwarden development scripts.

.DESCRIPTION
    Provides colored console output functions for consistent logging across
    development scripts.
#>

function Write-InfoLog {
    param([string]$Message)
    Write-Host "[INFO] " -ForegroundColor Blue -NoNewline
    Write-Host $Message
}

function Write-SuccessLog {
    param([string]$Message)
    Write-Host "[SUCCESS] " -ForegroundColor Green -NoNewline
    Write-Host $Message
}

function Write-WarningLog {
    param([string]$Message)
    Write-Host "[WARNING] " -ForegroundColor Yellow -NoNewline
    Write-Host $Message
}

function Write-ErrorLog {
    param([string]$Message)
    Write-Host "[ERROR] " -ForegroundColor Red -NoNewline
    Write-Host $Message
}
