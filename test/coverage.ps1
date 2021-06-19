param(
    [string][Alias('c')]$Configuration = "Release",
    [string][Alias('o')]$Output = "CoverageOutput",
    [string][Alias('rt')]$ReportType = "lcov"
)

function Install-Tools {
    dotnet tool restore
}

function Print-Environment {
    dotnet --version
}

function Prepare-Output {
    if (Test-Path -Path $Output) {
        Remove-Item $Output -Recurse
    }
}

function Run-Tests {
    dotnet test $PSScriptRoot/bitwarden.tests.sln /p:CoverletOutputFormatter="cobertura" --collect:"XPlat Code Coverage" --results-directory:"$Output" -c $Configuration

    dotnet tool run reportgenerator -reports:$Output/**/*.cobertura.xml -targetdir:$Output -reporttypes:"$ReportType"
}

Write-Host "Collecting Code Coverage"
Install-Tools
Print-Environment
Prepare-Output
Run-Tests
