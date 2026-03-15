#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Bitwarden Server Installation Script

.DESCRIPTION
    This script automates the setup process described in:
    https://contributing.bitwarden.com/getting-started/server/guide

    This script is idempotent and can be run multiple times safely.

.PARAMETER SkipDocker
    Skip Docker container setup

.PARAMETER SkipMigrations
    Skip database migrations

.PARAMETER SkipCertificates
    Skip certificate creation prompts

.PARAMETER SkipAzurite
    Skip Azurite storage setup

.EXAMPLE
    ./install.ps1
    Run full installation

.EXAMPLE
    ./install.ps1 -SkipDocker
    Run installation without starting Docker containers

.EXAMPLE
    ./install.ps1 -SkipAzurite
    Run installation without setting up Azurite storage
#>

param(
    [switch]$SkipDocker,
    [switch]$SkipMigrations,
    [switch]$SkipCertificates,
    [switch]$SkipAzurite
)

$ErrorActionPreference = "Stop"

# Get script and repository paths
$ScriptDir = Split-Path -Parent $PSCommandPath
$RepoRoot = Split-Path -Parent $ScriptDir

# Import logging functions
. "$ScriptDir/helpers/Write-Log.ps1"

# Check if a command exists
function Test-CommandExists {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Check prerequisites
function Test-Prerequisites {
    Write-InfoLog "Checking prerequisites..."

    $missingDeps = @()

    # Check for Docker
    if (-not (Test-CommandExists "docker")) {
        $missingDeps += "Docker Desktop"
    } else {
        $dockerVersion = docker --version
        Write-SuccessLog "Docker found: $dockerVersion"
    }

    # Check for .NET SDK
    if (-not (Test-CommandExists "dotnet")) {
        $missingDeps += ".NET 8.0 SDK"
    } else {
        $dotnetVersion = dotnet --version
        Write-SuccessLog ".NET SDK found: $dotnetVersion"

        # Check if it's version 8.x
        if (-not ($dotnetVersion -match "^8\.")) {
            Write-WarningLog ".NET SDK version 8.0 is recommended (found: $dotnetVersion)"
        }
    }

    # Check for PowerShell (we're already in it, but check version)
    $psVersion = $PSVersionTable.PSVersion
    if ($psVersion.Major -lt 7) {
        Write-WarningLog "PowerShell 7+ is recommended (found: $psVersion)"
    } else {
        Write-SuccessLog "PowerShell found: $psVersion"
    }

    # Check for Rust
    if (-not (Test-CommandExists "rustc")) {
        Write-WarningLog "Rust not found. It's recommended for some features."
        Write-InfoLog "Install from: https://rustup.rs/"
    } else {
        $rustVersion = rustc --version
        Write-SuccessLog "Rust found: $rustVersion"
    }

    # Check for Docker Compose
    if (Test-CommandExists "docker") {
        try {
            $composeVersion = docker compose version 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-SuccessLog "Docker Compose found: $composeVersion"
            } else {
                $missingDeps += "Docker Compose"
            }
        } catch {
            $missingDeps += "Docker Compose"
        }
    }

    # Check for dotnet-ef tool
    $efInstalled = $false
    try {
        dotnet ef *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-SuccessLog "Entity Framework Core tools found"
            $efInstalled = $true
        }
    } catch {
        # Will be installed later if needed
    }

    if (-not $efInstalled) {
        Write-InfoLog "Entity Framework Core tools will be installed if needed"
    }

    # Report missing dependencies
    if ($missingDeps.Count -gt 0) {
        Write-ErrorLog "Missing required dependencies:"
        foreach ($dep in $missingDeps) {
            Write-Host "  - $dep"
        }
        Write-Host ""
        Write-InfoLog "Please install the missing dependencies and run this script again."
        Write-InfoLog "See: https://contributing.bitwarden.com/getting-started/server/guide#prerequisites"
        exit 1
    }

    Write-SuccessLog "All prerequisites satisfied"
}

# Configure Git
function Set-GitConfiguration {
    Write-InfoLog "Configuring Git..."

    Set-Location $RepoRoot

    # Set up blame ignore file
    $currentBlame = git config --get blame.ignoreRevsFile 2>$null
    if ($currentBlame -ne ".git-blame-ignore-revs") {
        git config blame.ignoreRevsFile .git-blame-ignore-revs
        Write-SuccessLog "Git blame.ignoreRevsFile configured"
    } else {
        Write-InfoLog "Git blame.ignoreRevsFile already configured"
    }
}

# Setup Docker environment
function Initialize-DockerEnvironment {
    Write-InfoLog "Setting up Docker environment..."

    Set-Location $ScriptDir

    # Check if .env file exists
    if (-not (Test-Path .env)) {
        if (Test-Path .env.example) {
            Copy-Item .env.example .env
            Write-SuccessLog "Created .env file from .env.example"

            # Generate a strong random password
            $randomPassword = & "$ScriptDir/helpers/New-RandomPassword.ps1" -Length 20

            # Replace all password placeholders with the generated password
            $envContent = Get-Content .env -Raw
            $envContent = $envContent -replace "SET_A_PASSWORD_HERE_123", $randomPassword
            Set-Content .env $envContent -NoNewline

            Write-SuccessLog "Generated and set random passwords in .env file"
            Write-InfoLog "MSSQL_PASSWORD: $randomPassword"

        } else {
            Write-ErrorLog ".env.example not found. Cannot create .env file."
            exit 1
        }
    } else {
        Write-InfoLog ".env file already exists"
    }
}

# Start Docker containers
function Start-DockerServices {
    Write-InfoLog "Starting Docker containers..."

    Set-Location $ScriptDir

    # Check if Docker is running
    try {
        docker ps > $null 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorLog "Docker is not running. Please start Docker Desktop and try again."
            return $false
        }
    } catch {
        Write-ErrorLog "Docker is not running. Please start Docker Desktop and try again."
        return $false
    }

    # Check if containers are already running
    $runningContainers = docker compose ps --status running 2>&1
    if ($runningContainers -match "Up") {
        Write-InfoLog "Docker containers are already running"
        return $true
    }

    # Start containers
    Write-InfoLog "Starting cloud containers..."
    docker compose --profile cloud up -d

    if ($LASTEXITCODE -ne 0) {
        Write-ErrorLog "Failed to start Docker containers"
        return $false
    }

    Write-SuccessLog "Docker containers started"
    Write-InfoLog "MailCatcher available at: http://localhost:1080"
    Write-InfoLog "MSSQL available at: localhost:1433 (user: sa)"
    Write-InfoLog "Azurite (Blob) available at: http://localhost:10000"
    Write-InfoLog "Azurite (Queue) available at: http://localhost:10001"
    Write-InfoLog "Azurite (Table) available at: http://localhost:10002"

    # Wait for MSSQL to be ready
    Write-InfoLog "Waiting for MSSQL to be ready..."
    Start-Sleep -Seconds 5

    $maxAttempts = 30
    $attempt = 0

    # Get password from .env
    $envLines = Get-Content .env
    $mssqlPassword = ($envLines | Where-Object { $_ -match "^MSSQL_PASSWORD=" }) -replace "^MSSQL_PASSWORD=", ""

    while ($attempt -lt $maxAttempts) {
        try {
            $result = docker compose exec -T mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$mssqlPassword" -C -Q "SELECT 1" 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-SuccessLog "MSSQL is ready"
                break
            }
        } catch {
            # Continue waiting
        }
        $attempt++
        if ($attempt -eq $maxAttempts) {
            Write-WarningLog "MSSQL may not be ready yet. Continuing anyway..."
            break
        }
        Start-Sleep -Seconds 2
    }

    return $true
}

# Setup secrets.json
function Initialize-SecretsFile {
    param(
        [string]$CertificateThumbprint
    )

    Write-InfoLog "Setting up secrets.json..."

    Set-Location $ScriptDir

    if (-not (Test-Path secrets.json)) {
        Copy-Item secrets.json.example secrets.json
        Write-SuccessLog "Created secrets.json from secrets.json.example"

        $secretsContent = Get-Content secrets.json -Raw

        # Update connection string with password from .env
        if (Test-Path .env) {
            $envLines = Get-Content .env
            $mssqlPassword = ($envLines | Where-Object { $_ -match "^MSSQL_PASSWORD=" }) -replace "^MSSQL_PASSWORD=", ""

            if ($mssqlPassword -and $mssqlPassword -ne "SET_A_PASSWORD_HERE_123") {
                $secretsContent = $secretsContent -replace "SET_A_PASSWORD_HERE_123", $mssqlPassword
                Write-SuccessLog "Updated connection strings with MSSQL_PASSWORD from .env"
            }
        }

        # Update certificate thumbprints if provided
        if ($CertificateThumbprint) {
            $thumbprintNoSpaces = $CertificateThumbprint -replace '\s', ''
            $secretsContent = $secretsContent -replace '<your Identity certificate thumbprint with no spaces>', $thumbprintNoSpaces
            $secretsContent = $secretsContent -replace '<your Data Protection certificate thumbprint with no spaces>', $thumbprintNoSpaces
            Write-SuccessLog "Updated certificate thumbprints in secrets.json"
        }

        Set-Content secrets.json $secretsContent -NoNewline

        # Certificate thumbprint is required - fail if missing
        if (-not $CertificateThumbprint) {
            Write-ErrorLog "Certificate thumbprint is required but was not generated."
            Write-InfoLog "Run the certificate creation script to generate certificates."
            exit 1
        }

        # Installation ID/Key and License directory are only needed for self-host
        Write-InfoLog "NOTE: For SELF-HOST mode, you will need to configure in secrets.json:"
        Write-InfoLog "  1. Installation ID and Key (get from: https://bitwarden.com/host)"
        Write-InfoLog "  2. License directory path (full path to a directory for license files)"
        Write-Host ""
    } else {
        Write-InfoLog "secrets.json already exists"

        # Check if it still has placeholder values
        $secretsContent = Get-Content secrets.json -Raw
        if ($secretsContent -match "SET_A_PASSWORD_HERE_123|<your Installation Id>|<your Installation Key>") {
            Write-WarningLog "secrets.json contains placeholder values"
        }
    }
}

# Apply secrets to all projects
function Set-ProjectSecrets {
    Write-InfoLog "Applying secrets to all projects..."

    Set-Location $ScriptDir

    try {
        & "$ScriptDir/setup_secrets.ps1"
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorLog "Failed to apply secrets"
            return $false
        }
        Write-SuccessLog "Secrets applied to all projects"
        return $true
    } catch {
        Write-ErrorLog "Failed to apply secrets: $_"
        return $false
    }
}

# Install EF Core tools if needed
function Install-EFCoreTools {
    Write-InfoLog "Checking Entity Framework Core tools..."

    try {
        dotnet ef *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-InfoLog "Entity Framework Core tools already installed"
            return $true
        }
    } catch {
        # Not installed, continue to installation
    }

    Write-InfoLog "Installing Entity Framework Core tools..."
    dotnet tool install dotnet-ef -g

    if ($LASTEXITCODE -ne 0) {
        Write-ErrorLog "Failed to install Entity Framework Core tools"
        return $false
    }

    Write-SuccessLog "Entity Framework Core tools installed"
    return $true
}

# Setup Azurite storage
function Initialize-AzuriteStorage {
    Write-InfoLog "Setting up Azurite storage..."

    Set-Location $ScriptDir

    # Check if Az.Storage module is installed
    if (-not (Get-Module -ListAvailable -Name Az.Storage)) {
        Write-InfoLog "Az.Storage module not found. Installing..."
        try {
            Install-Module -Name Az.Storage -Force -AllowClobber -Scope CurrentUser
            Write-SuccessLog "Az.Storage module installed"
        } catch {
            Write-ErrorLog "Failed to install Az.Storage module: $_"
            Write-WarningLog "Skipping Azurite setup. You can run it manually later with: pwsh setup_azurite.ps1"
            return $false
        }
    }

    try {
        & "$ScriptDir/setup_azurite.ps1"
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorLog "Failed to setup Azurite storage"
            return $false
        }
        Write-SuccessLog "Azurite storage configured"
        return $true
    } catch {
        Write-ErrorLog "Failed to setup Azurite storage: $_"
        Write-WarningLog "You can run it manually later with: pwsh setup_azurite.ps1"
        return $false
    }
}

# Run database migrations
function Invoke-DatabaseMigrations {
    Write-InfoLog "Running database migrations..."

    Set-Location $ScriptDir

    try {
        & "$ScriptDir/migrate.ps1"
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorLog "Failed to run migrations"
            Write-InfoLog "Make sure MSSQL is running and secrets.json is properly configured"
            return $false
        }
        Write-SuccessLog "Database migrations completed"
        return $true
    } catch {
        Write-ErrorLog "Failed to run migrations: $_"
        Write-InfoLog "Make sure MSSQL is running and secrets.json is properly configured"
        return $false
    }
}

# Create development certificates
function New-DevelopmentCertificates {
    Write-InfoLog "Setting up development certificates..."

    Set-Location $ScriptDir

    try {
        $thumbprint = & "$ScriptDir/helpers/New-DevelopmentCertificate.ps1"
        if ($thumbprint) {
            Write-SuccessLog "Development certificates ready"
            return $thumbprint
        } else {
            Write-WarningLog "Could not retrieve certificate thumbprint"
            return $null
        }
    } catch {
        Write-ErrorLog "Failed to setup certificates: $_"
        Write-InfoLog "You can try manually with: pwsh $ScriptDir/helpers/New-DevelopmentCertificate.ps1"
        return $null
    }
}

# Verify installation
function Test-Installation {
    Write-InfoLog "Verifying installation..."

    Set-Location $RepoRoot

    # Check if dotnet restore works
    Write-InfoLog "Running dotnet restore..."
    try {
        dotnet restore > $null 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-SuccessLog "dotnet restore completed successfully"
        } else {
            Write-WarningLog "dotnet restore had issues (this may be normal if some dependencies are missing)"
        }
    } catch {
        Write-WarningLog "dotnet restore had issues (this may be normal if some dependencies are missing)"
    }

    # Check if Docker containers are running
    Set-Location $ScriptDir
    try {
        $runningContainers = docker compose ps --status running 2>&1
        if ($runningContainers -match "Up") {
            Write-SuccessLog "Docker containers are running"
        } else {
            Write-WarningLog "Docker containers are not running"
        }
    } catch {
        Write-WarningLog "Could not check Docker container status"
    }
}

# Main installation flow
function Start-Installation {
    Write-Host ""
    Write-InfoLog "================================================"
    Write-InfoLog "Bitwarden Server Development Installation Script"
    Write-InfoLog "================================================"
    Write-Host ""

    Test-Prerequisites
    Write-Host ""

    Set-GitConfiguration
    Write-Host ""

    # Create certificates first so we can populate thumbprints in secrets.json
    $certThumbprint = $null
    if (-not $SkipCertificates) {
        $certThumbprint = New-DevelopmentCertificates
        Write-Host ""
    } else {
        Write-InfoLog "Skipping certificate setup (--SkipCertificates specified)"
        Write-Host ""
    }

    if (-not $SkipDocker) {
        Initialize-DockerEnvironment
        Write-Host ""

        Start-DockerServices
        Write-Host ""
    } else {
        Write-InfoLog "Skipping Docker setup (--SkipDocker specified)"
        Write-Host ""
    }

    Initialize-SecretsFile -CertificateThumbprint $certThumbprint
    Write-Host ""

    Set-ProjectSecrets
    Write-Host ""

    if (-not $SkipAzurite) {
        Initialize-AzuriteStorage
        Write-Host ""
    } else {
        Write-InfoLog "Skipping Azurite setup (--SkipAzurite specified)"
        Write-Host ""
    }

    if (-not $SkipMigrations) {
        Invoke-DatabaseMigrations
        Write-Host ""
    } else {
        Write-InfoLog "Skipping database migrations (--SkipMigrations specified)"
        Write-Host ""
    }

    Write-SuccessLog "======================================"
    Write-SuccessLog "Installation completed!"
    Write-SuccessLog "======================================"
    Write-Host ""
    Write-InfoLog "Next steps:"
    Write-InfoLog "1. Download and install the Licensing Certificate - Dev:"
    Write-InfoLog "   Go to: https://vault.bitwarden.com/#/vault?itemId=7123e5d3-f837-4a8c-810a-a7ca00fe1fdd&action=view"
    Write-InfoLog "   - Log in to your company-issued Bitwarden account"
    Write-InfoLog "   - View attachments and download both files (dev.cer and dev.pfx)"
    Write-InfoLog "   - Double-click the downloaded certificate to install it"
    Write-InfoLog "   - Mac users: In Keychain Access, select 'Default Keychain > login' when saving"
    Write-InfoLog "   - Set the dev.cer certificate to 'Always Trust' in Keychain Access"
    Write-InfoLog "   - The dev.pfx file password can be found in the Licensing Certificate - Dev vault item"
    Write-Host ""
    Write-InfoLog "2. To start the Identity service:"
    Write-InfoLog "   cd $RepoRoot/src/Identity"
    Write-InfoLog "   dotnet run"
    Write-InfoLog "   Access at: http://localhost:33656/.well-known/openid-configuration"
    Write-Host ""
    Write-InfoLog "3. To start the API service:"
    Write-InfoLog "   cd $RepoRoot/src/Api"
    Write-InfoLog "   dotnet run"
    Write-InfoLog "   Access at: http://localhost:4000/alive"
    Write-Host ""
    Write-InfoLog "4. View emails at: http://localhost:1080"
    Write-Host ""
    Write-InfoLog "5. Azure Storage Emulator (Azurite) is running at:"
    Write-InfoLog "   - Blob: http://localhost:10000"
    Write-InfoLog "   - Queue: http://localhost:10001"
    Write-InfoLog "   - Table: http://localhost:10002"
    Write-Host ""
    Write-InfoLog "For debugging in Visual Studio or Rider, open the solution and start the projects."
    Write-InfoLog "See: https://contributing.bitwarden.com/getting-started/server/guide"
    Write-Host ""
}

# Run main function
Start-Installation
