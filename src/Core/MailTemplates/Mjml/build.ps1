#!/usr/bin/env pwsh
# Build all MJML files in subdirectories
param(
    [string]$InputDir = "emails",
    [string]$OutputDir = "out",
    [switch]$Watch,
    [switch]$Minify,
    [switch]$Clean
)

# Abort on any error
$ErrorActionPreference = "Stop"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-MjmlInstalled {
    try {
        $null = Get-Command "npx" -ErrorAction Stop
        $mjmlVersion = npx mjml --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "[OK] MJML found: $mjmlVersion" "Green"
            return $true
        }
    }
    catch {
        Write-ColorOutput "[ERROR] npx not found. Please install Node.js" "Red"
        return $false
    }

    Write-ColorOutput "[WARN] MJML not found. Installing..." "Yellow"
    npm install mjml
    return $true
}

function Get-MjmlFiles {
    param([string]$Directory)

    if (!(Test-Path $Directory)) {
        Write-ColorOutput "[ERROR] Directory not found: $Directory" "Red"
        return @()
    }

    $mjmlFiles = Get-ChildItem -Path $Directory -Filter "*.mjml" -Recurse | ForEach-Object {
        [PSCustomObject]@{
            FullPath = $_.FullName
            RelativePath = $_.FullName.Replace("$(Resolve-Path $Directory)\", "")
            Name = $_.BaseName
            Directory = $_.DirectoryName
        }
    }

    return $mjmlFiles
}

function Invoke-BuildMjmlFile {
    param(
        [PSCustomObject]$MjmlFile,
        [string]$BaseInputDir,
        [string]$BaseOutputDir,
        [bool]$MinifyOutput = $false
    )

    # Calculate relative path structure
    $relativePath = $MjmlFile.FullPath.Replace("$(Resolve-Path $BaseInputDir)\", "")
    $relativeDir = Split-Path $relativePath -Parent
    $outputSubDir = if ($relativeDir) { Join-Path $BaseOutputDir $relativeDir } else { $BaseOutputDir }
    $outputFile = Join-Path $outputSubDir "$($MjmlFile.Name).html.hbs"

    # Ensure output directory exists
    if (!(Test-Path $outputSubDir)) {
        New-Item -ItemType Directory -Path $outputSubDir -Force | Out-Null
        Write-ColorOutput "[INFO] Created directory: $outputSubDir" "Cyan"
    }

    Write-ColorOutput "[BUILD] Building: $($MjmlFile.FullPath) -> $($outputFile)" "White"

    # Build MJML command
    $mjmlArgs = @(
        "mjml"
        "`"$($MjmlFile.FullPath)`""
        "-o"
        "`"$outputFile`""
    )

    if ($MinifyOutput) {
        $mjmlArgs += "--config.minify=true"
    }

    try {
        Write-ColorOutput "        Command: npx $($mjmlArgs -join ' ')" "Gray"
        $output = npx @mjmlArgs 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "[OK] Built: $($MjmlFile.Name).mjml" "Green"
            return $true
        } else {
            Write-ColorOutput "[ERROR] Failed to build $($MjmlFile.Name).mjml" "Red"
            Write-ColorOutput "   Error: $output" "Red"
            return $false
        }
    }
    catch {
        Write-ColorOutput "[ERROR] Exception building $($MjmlFile.Name).mjml: $($_.Exception.Message)" "Red"
        return $false
    }
}

function Start-MjmlWatch {
    param(
        [string]$InputDirectory,
        [string]$OutputDirectory,
        [bool]$MinifyOutput = $false
    )

    Write-ColorOutput "[WATCH] Watching for changes in: $InputDirectory" "Magenta"
    Write-ColorOutput "        Press Ctrl+C to stop watching" "Gray"

    $lastBuildTime = @{}

    while ($true) {
        try {
            $mjmlFiles = Get-MjmlFiles -Directory $InputDirectory
            $hasChanges = $false

            foreach ($file in $mjmlFiles) {
                $fileInfo = Get-Item $file.FullPath
                $lastWrite = $fileInfo.LastWriteTime

                if (!$lastBuildTime.ContainsKey($file.FullPath) -or $lastBuildTime[$file.FullPath] -lt $lastWrite) {
                    Write-ColorOutput "[CHANGE] Change detected: $($file.RelativePath)" "Yellow"
                    $success = Invoke-BuildMjmlFile -MjmlFile $file -BaseInputDir $InputDirectory -BaseOutputDir $OutputDirectory -MinifyOutput $MinifyOutput

                    if ($success) {
                        $lastBuildTime[$file.FullPath] = $lastWrite
                    }
                    $hasChanges = $true
                }
            }

            if (!$hasChanges) {
                Start-Sleep -Seconds 1
            }
        }
        catch [System.Management.Automation.PipelineStoppedException] {
            Write-ColorOutput "`n[STOP] Watch mode stopped" "Yellow"
            break
        }
        catch {
            Write-ColorOutput "[ERROR] Error in watch mode: $($_.Exception.Message)" "Red"
            Start-Sleep -Seconds 2
        }
    }
}

function Invoke-CleanOutputDirectory {
    param([string]$Directory)

    if (Test-Path $Directory) {
        Write-ColorOutput "[CLEAN] Cleaning output directory: $Directory" "Yellow"
        Remove-Item -Path "$Directory\*" -Recurse -Force
        Write-ColorOutput "[OK] Output directory cleaned" "Green"
    }
}

# Main execution
function Main {
    Write-ColorOutput "`n[BUILD] MJML Builder Script" "Cyan"
    Write-ColorOutput "=========================" "Cyan"

    # Check if MJML is available
    if (!(Test-MjmlInstalled)) {
        exit 1
    }

    # Find all MJML files
    Write-ColorOutput "`n[SCAN] Scanning for MJML files in: $InputDir" "White"
    $mjmlFiles = Get-MjmlFiles -Directory $InputDir

    if ($mjmlFiles.Count -eq 0) {
        Write-ColorOutput "[WARN] No MJML files found in $InputDir" "Yellow"
        exit 0
    }

    Write-ColorOutput "[INFO] Found $($mjmlFiles.Count) MJML file(s)" "Green"

    # Create output directory
    if (!(Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
        Write-ColorOutput "[INFO] Created output directory: $OutputDir" "Cyan"
    }

    if ($Watch) {
        # Initial build
        Write-ColorOutput "`n[BUILD] Initial build..." "White"
        $successCount = 0
        foreach ($file in $mjmlFiles) {
            if (Invoke-BuildMjmlFile -MjmlFile $file -BaseInputDir $InputDir -BaseOutputDir $OutputDir -MinifyOutput $Minify) {
                $successCount++
            }
        }
        Write-ColorOutput "`n[OK] Initial build complete: $successCount/$($mjmlFiles.Count) files built successfully" "Green"

        # Start watching
        Start-MjmlWatch -InputDirectory $InputDir -OutputDirectory $OutputDir -MinifyOutput $Minify
    } else {
        # Build once
        Write-ColorOutput "`n[BUILD] Building files..." "White"
        $successCount = 0
        $failedFiles = @()

        foreach ($file in $mjmlFiles) {
            if (Invoke-BuildMjmlFile -MjmlFile $file -BaseInputDir $InputDir -BaseOutputDir $OutputDir -MinifyOutput $Minify) {
                $successCount++
            } else {
                $failedFiles += $file.RelativePath
            }
        }

        Write-ColorOutput "`n[SUMMARY] Build Summary:" "Cyan"
        Write-ColorOutput "          Success: $successCount" "Green"
        Write-ColorOutput "          Failed:  $($failedFiles.Count)" "Red"

        if ($failedFiles.Count -gt 0) {
            Write-ColorOutput "`n[ERROR] Failed files:" "Red"
            foreach ($file in $failedFiles) {
                Write-ColorOutput "        * $file" "Red"
            }
            exit 1
        }

        Write-ColorOutput "`n[SUCCESS] All files built successfully!" "Green"
    }
}

# Usage with clean feature
if ($Clean) {
    Invoke-CleanOutputDirectory -Directory $OutputDir
}

# Run the main function
Main