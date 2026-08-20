#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generate a strong random password.

.DESCRIPTION
    Generates a random password with configurable character requirements:
    - Latin uppercase letters (A through Z)
    - Latin lowercase letters (a through z)
    - Base 10 digits (0 through 9)
    - Non-alphanumeric characters: !, $, #, %

    By default, the password includes at least one character from each category.

.PARAMETER Length
    The length of the password to generate. Default is 20 characters. Minimum is 4.

.PARAMETER RequireUppercase
    Require at least one uppercase letter. Default is true.

.PARAMETER RequireLowercase
    Require at least one lowercase letter. Default is true.

.PARAMETER RequireDigits
    Require at least one digit. Default is true.

.PARAMETER RequireSpecial
    Require at least one special character. Default is true.

.EXAMPLE
    ./New-RandomPassword.ps1
    Generates a 20-character random password with all character types

.EXAMPLE
    ./New-RandomPassword.ps1 -Length 12 -RequireSpecial:$false
    Generates a 12-character random password without special characters

.OUTPUTS
    System.String
    A randomly generated password string
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateRange(4, 128)]
    [int]$Length = 20,

    [Parameter()]
    [bool]$RequireUppercase = $true,

    [Parameter()]
    [bool]$RequireLowercase = $true,

    [Parameter()]
    [bool]$RequireDigits = $true,

    [Parameter()]
    [bool]$RequireSpecial = $true
)

# Define character sets
$uppercase = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
$lowercase = 'abcdefghijklmnopqrstuvwxyz'
$digits = '0123456789'
$special = '!$#%'

# All characters are always available for random selection
$allChars = $uppercase + $lowercase + $digits + $special

# Build password ensuring required characters
$password = @()

if ($RequireUppercase) {
    $password += $uppercase[(Get-Random -Maximum $uppercase.Length)]
}

if ($RequireLowercase) {
    $password += $lowercase[(Get-Random -Maximum $lowercase.Length)]
}

if ($RequireDigits) {
    $password += $digits[(Get-Random -Maximum $digits.Length)]
}

if ($RequireSpecial) {
    $password += $special[(Get-Random -Maximum $special.Length)]
}

# Validate that length is sufficient for required characters
if ($Length -lt $password.Count) {
    throw "Length must be at least $($password.Count) to satisfy all requirements"
}

# Fill the rest with random characters from allowed categories
for ($i = $password.Count; $i -lt $Length; $i++) {
    $password += $allChars[(Get-Random -Maximum $allChars.Length)]
}

# Shuffle the password to avoid predictable patterns
$shuffled = $password | Sort-Object { Get-Random }
$result = -join $shuffled

# Output the password
Write-Output $result
