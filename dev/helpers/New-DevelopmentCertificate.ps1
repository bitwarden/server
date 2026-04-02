#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Create development certificates for Bitwarden server.

.DESCRIPTION
    Creates self-signed development certificates for Identity Server and Data Protection.
    The certificates are valid for 10 years and stored in the dev directory.

    This script works across Windows, macOS, and Linux by calling platform-specific
    certificate creation scripts.

.PARAMETER Force
    Force recreation of certificates even if they already exist.

.EXAMPLE
    ./New-DevelopmentCertificate.ps1
    Creates certificates if they don't exist and returns the thumbprint

.EXAMPLE
    ./New-DevelopmentCertificate.ps1 -Force
    Force recreate certificates and return the thumbprint

.EXAMPLE
    $thumbprint = ./New-DevelopmentCertificate.ps1
    Store the certificate thumbprint in a variable

.OUTPUTS
    System.String
    The certificate thumbprint (SHA1 hash) formatted with spaces
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Get the dev directory (parent of helpers)
$DevDir = Split-Path -Parent $PSScriptRoot
Set-Location $DevDir

# Check if certificates already exist
if ($IsWindows) {
    $existingCert = Get-ChildItem -Path Cert:\CurrentUser\My |
                    Where-Object { $_.Subject -eq "CN=Bitwarden Identity Server Dev" } |
                    Select-Object -First 1
    $certificatesExist = $null -ne $existingCert
} else {
    $certificatesExist = (Test-Path identity_server_dev.crt) -and
                        (Test-Path identity_server_dev.key)
}

# If certificates exist and not forcing recreation, return existing thumbprint
if ($certificatesExist -and -not $Force) {
    return & "$PSScriptRoot/Get-CertificateThumbprint.ps1"
}

# Create certificates using platform-specific scripts
if ($IsWindows) {
    if ($Force -and $existingCert) {
        Remove-Item -Path "Cert:\CurrentUser\My\$($existingCert.Thumbprint)" -Force
    }

    & "$PSScriptRoot/create_certificates_windows.ps1"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create certificates"
        exit 1
    }
} elseif ($IsMacOS) {
    if ($Force) {
        Remove-Item identity_server_dev.pfx, identity_server_dev.crt, identity_server_dev.key -ErrorAction SilentlyContinue
    }

    bash "$PSScriptRoot/create_certificates_mac.sh"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create certificates"
        exit 1
    }
} elseif ($IsLinux) {
    if ($Force) {
        Remove-Item identity_server_dev.crt, identity_server_dev.key -ErrorAction SilentlyContinue
    }

    bash "$PSScriptRoot/create_certificates_linux.sh"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create certificates"
        exit 1
    }
} else {
    Write-Error "Unsupported operating system"
    exit 1
}

# Return the certificate thumbprint
return & "$PSScriptRoot/Get-CertificateThumbprint.ps1"
