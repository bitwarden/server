param(
    [string][Alias('c')]$configuration = "Release",
    [string][Alias('o')]$output = "CoverageOutput"
)

function Install-Tools {
    dotnet tool restore
}

function Print-Environment {
    dotnet --version
    dotnet tool run coverlet --version
}

function Prepare-Output {
    if (Test-Path -Path $output) {
        Remove-Item $output -Recurse
    }
}

function Run-Tests {
    $testProjects = Get-ChildItem -Filter "*.Test.csproj" -Recurse

    foreach ($testProject in $testProjects)
    {
        dotnet test $testProject.FullName /p:CoverletOutputFormatter="cobertura" --collect:"XPlat Code Coverage" --results-directory:"$output" -c $configuration
    }

    dotnet tool run reportgenerator -reports:$output/**/*.cobertura.xml -targetdir:$output -reporttypes:"Html;Cobertura"
}

Write-Host "Collecting Code Coverage"
Install-Tools
Print-Environment
Prepare-Output
Run-Tests