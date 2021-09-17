Helper docker-compose files.

- `docker-compose.azurite.yml`: Azure Storage simulator, simplifies testing of cloud related interfaces.
- `docker-compose.mailcatcher.yml`: Local SMTP server with web interface for reading emails. The web interface can be accessed at http://localhost:1080
- `docker-compose.mssql.yml`: Microsoft SQL Server instance.

We provide some helper scripts, note that some functionality requires the [`Az` powershell module](https://docs.microsoft.com/en-us/powershell/azure/new-azureps-module-az?view=azps-6.4.0), which can be installed using `Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force`.

- `migrate.ps1`: Creates the `vault_dev` database for the `mssql` container, and runs all pending migrations. (Last migration is stored in `.data/mssql/last_migration`).
- `setup_azurite.ps1`: Configures Azurite with the required containers, queus and tables. Can be safely re-run.
- `create_certificates_windows.ps1`: Creates and adds the certificats to the users personal certificate store on Windows.
- `create_certificates_openssl.sh`: Creates the certificates using openssl for mac or linux.
