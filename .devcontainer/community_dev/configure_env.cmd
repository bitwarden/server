function Test-Env {
  if (!(Test-Path "./dev/.env")) {
    Write-Host "Creating ./dev/.env with default key-values"
    Set-Content -Path "./dev/.env" -Value "COMPOSE_PROJECT_NAME=bitwarden_server"
    Add-Content -Path "./dev/.env" -Value "MSSQL_SA_PASSWORD=d3vP@ssw0rd"
  }
  else {
    Write-Host "Found ./dev/.env"
    Copy-MSSQLVar
  }
}

function Copy-MSSQLVar {
  if (Select-String -Path "./dev/.env" -Pattern "MSSQL_SA_PASSWORD" -Quiet) {
    Write-Host "MSSQL_SA_PASSWORD already exists in ./dev/.env"
  }
  else {
    Write-Host "Copying MSSQL_PASSWORD to MSSQL_SA_PASSWORD"
    $DB_PASSWORD = Get-Content "./dev/.env" | Where-Object { $_ -match "^MSSQL_PASSWORD=" }
    $DB_PASSWORD -replace "MSSQL_PASSWORD", "MSSQL_SA_PASSWORD" | Out-File -Append ./dev/.env
  }
}

Test-Env
