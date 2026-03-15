#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Get the thumbprint of the Bitwarden development certificate.

.DESCRIPTION
    Retrieves the SHA1 thumbprint of the Identity Server development certificate.
    Works on both Windows (from certificate store) and Unix (from certificate files).

.EXAMPLE
    ./Get-CertificateThumbprint.ps1
    Returns the certificate thumbprint

.OUTPUTS
    System.String
    The certificate thumbprint formatted with spaces (e.g., "AB CD EF ...")
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Get the dev directory (parent of helpers)
$DevDir = Split-Path -Parent $PSScriptRoot
Set-Location $DevDir

# Helper function to format thumbprint
function Format-Thumbprint {
    param([string]$Thumbprint)
    $clean = $Thumbprint -replace '[^0-9A-Fa-f]', ''
    $formatted = $clean.ToUpper() -replace '(.{2})', '$1 '
    return $formatted.Trim()
}

try {
    if ($IsWindows) {
        # Windows: Get from certificate store
        $cert = Get-ChildItem -Path Cert:\CurrentUser\My |
                Where-Object { $_.Subject -eq "CN=Bitwarden Identity Server Dev" } |
                Select-Object -First 1

        if (-not $cert) {
            Write-Error "Development certificate not found in certificate store. Please create it first."
            exit 1
        }

        $rawThumbprint = $cert.Thumbprint
    } else {
        # Unix: Get from certificate file
        if (-not (Test-Path identity_server_dev.crt)) {
            Write-Error "Development certificate file not found. Please create it first."
            exit 1
        }

        if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
            Write-Error "OpenSSL is required but not found. Please install OpenSSL."
            exit 1
        }

        $thumbprintOutput = openssl x509 -in identity_server_dev.crt -outform der 2>$null | openssl dgst -sha1 2>$null

        if ($thumbprintOutput -match '([0-9A-Fa-f]{40})') {
            $rawThumbprint = $matches[1]
        } else {
            Write-Error "Failed to extract certificate thumbprint"
            exit 1
        }
    }

    return Format-Thumbprint -Thumbprint $rawThumbprint
} catch {
    Write-Error "Failed to retrieve certificate thumbprint: $_"
    exit 1
}
