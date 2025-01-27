cd ..
docker compose down bitwardenserver

docker volume rm bitwardenserver_mssql_dev_data
docker volume rm bitwardenserver_postgres_dev_data
docker volume rm bitwardenserver_mysql_dev_data

docker compose --profile cloud --profile mail up -d
pwsh setup_azurite.ps1
pwsh setup_secrets.ps1 -clear

docker compose --profile postgres up -d
docker compose --profile mysql up -d

pwsh migrate.ps1 -all

docker compose --profile idp up -d
