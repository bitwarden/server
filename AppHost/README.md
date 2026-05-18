# Aspire AppHost

.NET Aspire orchestrates the entire Bitwarden server local development environment — SQL Server,
Azure Storage (Azurite), MailCatcher, and all five application services — from a single command,
replacing the manual docker-compose workflow.

## Prerequisites

| Requirement            | Notes                                                                                                                            |
|------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| .NET SDK 10            | Required by Aspire                                                                                                               |
| Docker Desktop         | Runs SQL Server, Azurite, and MailCatcher containers                                                                             |
| PowerShell (`pwsh`)    | Used by migration and secrets scripts                                                                                            |
| Completed server setup | `dev/secrets.json` must exist — follow the [setup guide](https://contributing.bitwarden.com/getting-started/server/guide/) first |

## Quick Start

```bash
cd AppHost
dotnet run
```

The Aspire dashboard opens automatically in your browser. All resources start in dependency order;
the services wait for the database and secrets setup to finish before launching.

## What Gets Started

| Resource            | Type                      | Purpose                                                                   |
|---------------------|---------------------------|---------------------------------------------------------------------------|
| `setup-secrets`     | Executable                | Runs `dev/setup_secrets.ps1` — applies `dev/secrets.json` to all projects |
| `mssql`             | SQL Server 2022 container | Persistent data volume, port 1433                                         |
| `run-db-migrations` | Executable                | Runs `dev/migrate.ps1` against `vault_dev` (or `self_host_dev`)           |
| `azurite`           | Azure Storage emulator    | Blob :10000 · Queue :10001 · Table :10002                                 |
| `azurite-setup`     | Executable                | Runs `dev/setup_azurite.ps1` after Azurite is ready                       |
| `mailcatcher`       | Container                 | SMTP :10250 · Web UI :1080                                                |
| `admin`             | .NET project              | Admin portal                                                              |
| `api`               | .NET project              | Main API (waits for Azurite)                                              |
| `billing`           | .NET project              | Billing service                                                           |
| `identity`          | .NET project              | Identity / auth service                                                   |
| `notifications`     | .NET project              | Notifications service (waits for Azurite)                                 |

## Configuration

All configuration lives in `appsettings.Development.json`(see [Security](#security--do-not-commit-local-settings-or-tokens)).

### Service Ports

Each service's `BasePort` is pre-filled in `appsettings.Development.json` to match the port
defined in each service's own `Properties/launchSettings.json`. No action is needed unless a port
conflicts with something else on your machine — in that case override it via user secrets:

```bash
dotnet user-secrets set "Services:api:BasePort" "4001"
```

### Database Password

```bash
dotnet user-secrets set "Database:Password" "<your-sa-password>"
```

### Full Configuration Reference

| Key                         | Default                            | Description                                                                          |
|-----------------------------|------------------------------------|--------------------------------------------------------------------------------------|
| `SelfHost`                  | `false`                            | Switch to self-hosted mode (see [Self-Hosted Mode](#self-hosted-mode))               |
| `ClientsPath`               | `../../clients/apps`               | Path to the `clients` repo's `apps/` directory (used by the Node.js plugin)          |
| `WorkingDirectory`          | `../dev`                           | Directory where dev scripts are resolved                                             |
| `Services:<name>:BasePort`  | see `appsettings.Development.json` | HTTP port for each service; pre-filled to match each service's `launchSettings.json` |
| `Database:Image`            | `mssql/server:2022-latest`         | Docker image for SQL Server                                                          |
| `Database:Port`             | `1433`                             | Host port mapped to the SQL Server container                                         |
| `Database:Password`         | _(empty)_                          | SA password for the SQL Server container                                             |
| `Database:SelfHostPassword` | _(empty)_                          | SA password used in self-hosted mode                                                 |
| `Scripts:DbMigration`       | `migrate.ps1`                      | Migration script filename (relative to `WorkingDirectory`)                           |
| `Scripts:AzuriteSetup`      | `setup_azurite.ps1`                | Azurite setup script filename                                                        |
| `Scripts:SecretsSetup`      | `setup_secrets.ps1`                | Secrets setup script filename                                                        |
| `MailCatcher:Image`         | `sj26/mailcatcher:latest`          | Docker image for MailCatcher                                                         |
| `MailCatcher:SmtpPort`      | `10250`                            | Host SMTP port                                                                       |
| `MailCatcher:WebPort`       | `1080`                             | MailCatcher web UI port                                                              |
| `NgrokAuthToken`            | _(empty)_                          | ngrok auth token (used only when ngrok plugin is enabled)                            |
| `WebFrontend:Port`          | `8080`                             | Web frontend port (used only when Node.js plugin is enabled)                         |
| `WebFrontend:Url`           | `https://bitwarden.test:8080`      | Web frontend URL shown in the dashboard                                              |

## Optional Features

### Web Frontend (Node.js community plugin)

Runs the web client alongside the server services. Requires the Bitwarden
[clients](https://github.com/bitwarden/clients) repo cloned as a sibling to `server`.

1. Create an `AppHost.csproj.user` file next to `AppHost.csproj` (it is covered by `.gitignore`):

   ```xml
   <Project>
     <PropertyGroup>
       <EnableNodeJsCommunityPlugin>true</EnableNodeJsCommunityPlugin>
     </PropertyGroup>
   </Project>
   ```

2. If the clients repo is not at `../../clients/apps`, override the path:

   ```bash
   dotnet user-secrets set "ClientsPath" "<path/to/clients/apps>"
   ```

3. Run `dotnet run` as normal. The `web-frontend` resource starts with **explicit start** — open
   the Aspire dashboard and start it manually when you're ready.

### Ngrok (Billing Webhook Tunneling)

Exposes the billing service through a public ngrok tunnel, useful for testing Stripe webhooks
locally.

1. Create an `AppHost.csproj.user` file next to `AppHost.csproj` (it is covered by `.gitignore`):

   ```xml
   <Project>
     <PropertyGroup>
       <EnableNgrokCommunityPlugin>true</EnableNgrokCommunityPlugin>
     </PropertyGroup>
   </Project>
   ```

2. Set your ngrok auth token:

   ```bash
   dotnet user-secrets set "NgrokAuthToken" "<your-ngrok-auth-token>"
   ```

3. The `billing-webhook-ngrok-endpoint` resource starts with **explicit start** — launch it from
   the Aspire dashboard when you need the tunnel active.

## Dynamic Additional Projects

Load an extra project into the orchestration without touching source files — useful for temporary
integrations or in-progress work:

```bash
# Add a project
dotnet user-secrets set "AdditionalProjects:<name>:Path" "<relative/path/to/Project.csproj>"

# Optionally wire it as a reference into an existing service
dotnet user-secrets set "AdditionalProjects:<name>:ReferencedBy:0" "api"
```

Replace `<name>` with any identifier you choose. Multiple `ReferencedBy` entries are indexed (`0`,
`1`, `2`, …).

## Self-Hosted Mode

Switch to a self-hosted database configuration:

```bash
dotnet user-secrets set "SelfHost" "true"
dotnet user-secrets set "Database:SelfHostPassword" "<password>"
```

In self-hosted mode:
- The database name changes from `vault_dev` to `self_host_dev`
- The migration script receives the `-self-hosted` flag
- Each service's effective port becomes `BasePort + 1`

## Aspire Dashboard

The dashboard opens automatically when you run the AppHost. You can also navigate to it directly:

| Profile         | URL                       |
|-----------------|---------------------------|
| HTTPS (default) | `https://localhost:17271` |
| HTTP            | `http://localhost:15055`  |

The dashboard shows live resource status, structured logs, distributed traces, and environment
variables for every resource.

## Security — Do Not Commit Local Settings or Tokens

> **Warning:** Never commit local configuration values or secrets to the repository.

- `appsettings.Development.json` is checked in with intentionally empty defaults. Local overrides
  belong in **user secrets**, not in that file — edits to it will appear in `git diff` and risk
  accidental commit.
- `Database:Password`, `Database:SelfHostPassword`, and `NgrokAuthToken` are sensitive — always
  store them with `dotnet user-secrets`, never in any `appsettings.*.json` file.
- User secrets are stored outside the repo in your OS profile, keyed by the `UserSecretsId` in
  `AppHost.csproj`, and are never tracked by git.
- If you create an `appsettings.local.json`, add it to `.gitignore` before writing any values
  to it.

## Troubleshooting

| Symptom                          | Fix                                                                                   |
|----------------------------------|---------------------------------------------------------------------------------------|
| Secrets not applied to services  | Re-run `setup-secrets` from the Aspire dashboard, or verify `dev/secrets.json` exists |
| SQL Server container won't start | Confirm Docker Desktop is running and port 1433 is free                               |
| Migrations fail immediately      | Ensure `pwsh` (PowerShell) is on your `$PATH`                                         |
| Port conflicts on startup        | Set the conflicting `Services:<name>:BasePort` to a free port via user secrets        |
| Services stuck waiting           | Check the dashboard logs for `setup-secrets` or `run-db-migrations` errors            |
