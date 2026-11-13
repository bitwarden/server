# Aspire AppHost

.NET Aspire orchestrates the entire Bitwarden server local development environment — supporting
infrastructure and the Bitwarden application services — from a single command, replacing the
manual docker-compose workflow.

## Prerequisites

| Requirement            | Notes                                                                                                                            |
|------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| .NET SDK 10            | Required by Aspire                                                                                                               |
| Docker Desktop         | Runs the supporting infrastructure containers                                                                                    |
| PowerShell (`pwsh`)    | Used by migration and secrets scripts                                                                                            |
| Completed server setup | `dev/secrets.json` must exist — follow the [setup guide](https://contributing.bitwarden.com/getting-started/server/guide/) first |

## Quick Start

```bash
cd AppHost
dotnet run
```

The Aspire dashboard opens automatically in your browser. All resources start in dependency order;
the services wait for the databases and secrets setup to finish before launching.

## What Gets Started

| Resource                       | Type                      | Purpose                                                                                                                    |
|--------------------------------|---------------------------|----------------------------------------------------------------------------------------------------------------------------|
| `setup-secrets`                | Executable                | Runs `dev/setup_secrets.ps1` — applies `dev/secrets.json` to all projects                                                  |
| `mssql`                        | SQL Server 2025 container | Persistent data volume, port 1433 (see [Databases](#databases))                                                            |
| `mssql-vault-dev`              | Database                  | `vault_dev` (or `vault_dev_self_host`) inside `mssql`                                                                      |
| `postgres`, `mysql`, `mariadb` | Containers                | Off by default; each runs with its `<engine>-vault-dev` database when its `Mode` is `Container`                            |
| `migrate-mssql`                | Executable                | Runs `dev/migrate.ps1` against `vault_dev`; starts automatically when `mssql` runs in cloud mode                           |
| `migrate-mssql-selfhost`       | Executable                | Runs `dev/migrate.ps1 -selfhost` against `vault_dev_self_host`; starts automatically when `mssql` runs in self-hosted mode |
| `migrate-<engine>`             | Executable                | Runs `dev/migrate.ps1 -<engine>` for each enabled engine (see [Migrations](#migrations))                                   |
| `azurite`                      | Azure Storage emulator    | Blob :10000 · Queue :10001 · Table :10002 · persistent data volume                                                         |
| `azurite-setup`                | Executable                | Runs `dev/setup_azurite.ps1` after Azurite is ready                                                                        |
| `mailcatcher`                  | Container                 | SMTP :10250 · Web UI :1080                                                                                                 |
| `redis`                        | Container                 | Redis with AOF persistence, port 6379                                                                                      |
| `idp`                          | SimpleSAMLphp container   | SAML IdP for SSO testing (**explicit start** from the dashboard)                                                           |
| `admin`                        | .NET project              | Admin portal                                                                                                               |
| `api`                          | .NET project              | Main API (waits for Azurite)                                                                                               |
| `billing`                      | .NET project              | Billing service                                                                                                            |
| `events`                       | .NET project              | Events service (waits for Azurite)                                                                                         |
| `eventsProcessor`              | .NET project              | Events processor (waits for Azurite)                                                                                       |
| `icons`                        | .NET project              | Icons service                                                                                                              |
| `identity`                     | .NET project              | Identity / auth service                                                                                                    |
| `notifications`                | .NET project              | Notifications service (waits for Azurite)                                                                                  |
| `scim`                         | .NET project              | SCIM provisioning service                                                                                                  |
| `sso`                          | .NET project              | SSO service                                                                                                                |

## Configuration

The AppHost reads configuration from these sources, in load order; a later source overrides the same
key from an earlier one:

1. `appsettings.json`
2. `appsettings.Development.json` (checked in, holds the defaults)
3. User secrets
4. Environment variables
5. Command-line arguments
6. `AppHost/appsettings.local.json` (optional, gitignored)

The local file loads last, so it overrides user secrets, environment variables and command-line
arguments for any key it sets (see [Security](#security--do-not-commit-local-settings-or-tokens)
and [Git Worktrees](#git-worktrees)).

### Service Ports

Each service's `BasePort` is pre-filled in `appsettings.Development.json` to match the port
defined in each service's own `Properties/launchSettings.json`. No action is needed unless a port
conflicts with something else on your machine — in that case override it via user secrets:

```bash
dotnet user-secrets set "Services:api:BasePort" "4001"
```

### Database Passwords

The AppHost takes each database container's password from the connection string the services use in
`dev/secrets.json`, such as `globalSettings:sqlServer:connectionString` for SQL Server. There is no
AppHost password to set (see [Databases](#databases)).

### Full Configuration Reference

| Key                             | Default                            | Description                                                                                            |
|---------------------------------|------------------------------------|--------------------------------------------------------------------------------------------------------|
| `SelfHost`                      | `false`                            | Switch to self-hosted mode (see [Self-Hosted Mode](#self-hosted-mode))                                 |
| `ClientsPath`                   | `../../clients/apps`               | Path to the `clients` repo's `apps/` directory (see [Git Worktrees](#git-worktrees))                   |
| `WorkingDirectory`              | `../dev`                           | Directory where dev scripts are resolved                                                               |
| `Services:<name>:BasePort`      | see `appsettings.Development.json` | HTTP port for each service; pre-filled to match each service's `launchSettings.json`                   |
| `Databases:MsSql:Mode`          | `Container`                        | `Container` or `External` (see [Databases](#databases))                                                |
| `Databases:MsSql:Image`         | `mssql/server:2025-latest`         | Docker image for SQL Server                                                                            |
| `Databases:MsSql:Port`          | `1433`                             | Host port mapped to the SQL Server container                                                           |
| `Databases:MsSql:ContainerName` | _(empty)_                          | Container name; empty keeps Aspire's (see [Container Names and Volumes](#container-names-and-volumes)) |
| `Databases:MsSql:VolumeName`    | _(empty)_                          | Data volume name; empty keeps Aspire's                                                                 |
| `Databases:<engine>:*`          | see [Databases](#databases)        | Same keys for `Postgres`, `MySql` and `MariaDb`; `Sqlite` has only `Mode`                              |
| `Scripts:DbMigration`           | `migrate.ps1`                      | Migration script filename (relative to `WorkingDirectory`)                                             |
| `Scripts:AzuriteSetup`          | `setup_azurite.ps1`                | Azurite setup script filename                                                                          |
| `Scripts:SecretsSetup`          | `setup_secrets.ps1`                | Secrets setup script filename                                                                          |
| `MailCatcher:Image`             | `sj26/mailcatcher:latest`          | Docker image for MailCatcher                                                                           |
| `MailCatcher:SmtpPort`          | `10250`                            | Host SMTP port                                                                                         |
| `MailCatcher:WebPort`           | `1080`                             | MailCatcher web UI port                                                                                |
| `NgrokAuthToken`                | _(empty)_                          | ngrok auth token (used only when ngrok plugin is enabled)                                              |
| `Parameters:sso-org-id`         | `yourOrgIdHere`                    | Org ID for SAML SSO testing; the IdP builds its SP entity ID and ACS URL from it                       |
| `WebFrontend:Port`              | `8080`                             | Web frontend port (incremented by 1 in self-hosted mode)                                               |
| `WebFrontend:Url`               | `https://bitwarden.test`           | Web frontend base URL; the resolved port is appended automatically                                     |

## Databases

Each engine has a section under `Databases` with a `Mode`:

| Mode        | Effect                                                                                                                                                            |
|-------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Disabled`  | Nothing starts and no migration resource exists. Default for `Postgres`, `MySql`, `MariaDb` and `Sqlite`; not allowed for `MsSql`                                 |
| `Container` | Aspire runs the engine in a persistent container and migrates it on start. Default for `MsSql`; not allowed for `Sqlite`                                          |
| `External`  | The engine runs outside Aspire, such as a compose container or the SQLite file. No container starts; the migration resource stays (see [Migrations](#migrations)) |

| Section              | Image                      | Port   | User       |
|----------------------|----------------------------|--------|------------|
| `Databases:MsSql`    | `mssql/server:2025-latest` | `1433` | `sa`       |
| `Databases:Postgres` | `postgres:14`              | `5432` | `postgres` |
| `Databases:MySql`    | `mysql:8.0`                | `3306` | `root`     |
| `Databases:MariaDb`  | `mariadb:12`               | `4306` | `root`     |
| `Databases:Sqlite`   | none                       | none   | none       |

`MsSql`, `Postgres`, `MySql` and `MariaDb` take `Mode`, `Image`, `Port`, `ContainerName` and
`VolumeName`. `Sqlite` takes only `Mode`. In `Container` mode, `Image` and `Port` must be set, or the
AppHost stops with `Missing required configuration: <key>` before any resource starts. `MySql` and
`MariaDb` cannot both be enabled: `dev/migrate.ps1` migrates both through
`globalSettings:mySql:connectionString`.

The images, ports and users match `dev/docker-compose.yml`, so the connection strings in an existing
`dev/secrets.json` work. MariaDB is the exception: the AppHost's MariaDB runs as `root`, as CI does, not
as compose's `maria` user, so `globalSettings:mySql:connectionString` must connect as `root`.

To run PostgreSQL next to SQL Server, with the password from `globalSettings:postgreSql:connectionString`:

```bash
dotnet user-secrets set "Databases:Postgres:Mode" "Container"
```

Which database the services use is `globalSettings:databaseProvider` in `dev/secrets.json`, not an
AppHost setting. The AppHost cannot tell which one that file selects, so every service waits for every
`Container`-mode database.

Password rules:

- Each container's password comes from its engine's connection string in `dev/secrets.json`:
  `globalSettings:sqlServer`, `globalSettings:postgreSql` or `globalSettings:mySql` (`MySql` and
  `MariaDb` share it). In self-hosted mode, a `dev:selfHostOverride:globalSettings:*` connection string
  wins when it is set, as it does for the services. If the file, the connection string or its password
  is missing, the AppHost stops before any resource starts.
- A data volume takes the password only when it is first initialized, and the health check uses the
  current one on every start. A reused volume keeps the password it was created with, so changing the
  password in `dev/secrets.json` afterwards leaves the services waiting.
- One SQL Server container serves both modes, so the self-hosted connection string must use the same
  password as the cloud one unless self-hosted mode points at a different `VolumeName`.

### Container Names and Volumes

Aspire names a persistent container `<resource>-<hash>` and a data volume
`<application>-<hash>-<resource>-data`, where the hash comes from the AppHost project path (see
[container naming](https://aspire.dev/app-host/resource-lifetimes/#container-naming-and-uniqueness) and
[volume naming](https://aspire.dev/reference/api/csharp/aspire.hosting/volumenamegenerator/methods/#generate-iresourcebuilder-t-string)).
Every checkout, including each worktree, therefore gets its own containers and an empty database.
`ContainerName` and `VolumeName` override both:

- Set them in user secrets to share one container and its data across every worktree.
- Set them in `AppHost/appsettings.local.json` for values that belong to one worktree:

  ```json
  {
    "Databases": {
      "MsSql": { "ContainerName": "<container-name>", "VolumeName": "<volume-name>" }
    }
  }
  ```

- Aspire manages only containers it created (see
  [custom container names](https://aspire.dev/app-host/resource-lifetimes/#custom-container-names)). A
  container that docker compose created under the same name is not adopted; to keep using a running
  compose container, use `External` mode instead.
- `VolumeName` adopts existing data: stop the compose container, then point `VolumeName` at its volume,
  such as `bitwardenserver_mssql_dev_data`, `bitwardenserver_postgres_dev_data` or
  `bitwardenserver_mysql_dev_data`. The volume keeps its existing password, so the connection string in
  `dev/secrets.json` must use it (see the password rules above).
- Never adopt `bitwardenserver_mariadb_dev_data`: compose initialized it with a random root password and
  the `maria` user, and the AppHost's MariaDB connects as `root`.

### Migrations

Each migration resource runs `pwsh -File migrate.ps1 <switch>` from `WorkingDirectory` and waits for
`setup-secrets` to finish, because `dev/migrate.ps1` reads its connection strings from the Api project's
user secrets. A migration that does not start automatically can be started from the dashboard.

| Resource                 | Switch      | Exists when                  | Starts automatically when              | Also waits for                      |
|--------------------------|-------------|------------------------------|----------------------------------------|-------------------------------------|
| `migrate-mssql`          | (none)      | always                       | `MsSql` is `Container`, `SelfHost` off | `mssql-vault-dev` if `Container`    |
| `migrate-mssql-selfhost` | `-selfhost` | always                       | `MsSql` is `Container`, `SelfHost` on  | `mssql-vault-dev` if `Container`    |
| `migrate-postgres`       | `-postgres` | `Postgres` is not `Disabled` | `Postgres` is `Container`              | `postgres-vault-dev` if `Container` |
| `migrate-mysql`          | `-mysql`    | `MySql` is not `Disabled`    | `MySql` is `Container`                 | `mysql-vault-dev` if `Container`    |
| `migrate-mariadb`        | `-mariadb`  | `MariaDb` is not `Disabled`  | `MariaDb` is `Container`               | `mariadb-vault-dev` if `Container`  |
| `migrate-sqlite`         | `-sqlite`   | `Sqlite` is `External`       | always                                 | nothing else                        |

Things to know about `dev/migrate.ps1`:

- `-selfhost` only changes the SQL Server migration.
- When `dotnet ef` is missing, the script runs `dotnet tool install dotnet-ef -g`.
- Each Entity Framework migration rebuilds its migrations project on every AppHost start.
- The directory in SQLite's `Data Source` must exist; `dev/db/` is not in a fresh clone.

### Upgrading from `Database:*`

The `Database` section was renamed to `Databases:MsSql`. The AppHost no longer reads
`Database:Password` or `Database:SelfHostPassword`, because the passwords come from `dev/secrets.json`
(see [Databases](#databases)). If those are the only keys you set, there is nothing to change.

If you overrode `Database:Port` or `Database:Image`, the AppHost stops with a message naming the key
until you set the new one, from the `AppHost` directory:

```bash
dotnet user-secrets set "Databases:MsSql:Port" "<port>"
dotnet user-secrets set "Databases:MsSql:Image" "<image>"
```

`Database:Type` has no replacement. User secrets are shared by every checkout, and a checkout without
this change still reads `Database:*`, so remove the old keys once no checkout needs them:

```bash
dotnet user-secrets remove "Database:Password"
dotnet user-secrets remove "Database:SelfHostPassword"
dotnet user-secrets remove "Database:Port"
dotnet user-secrets remove "Database:Image"
dotnet user-secrets remove "Database:Type"
```

## Optional Features

### Web Frontend

Runs the web client alongside the server services. Requires the Bitwarden
[clients](https://github.com/bitwarden/clients) repo cloned as a sibling to `server`.

1. If the clients repo is not at `../../clients/apps`, override the path:

   ```bash
   dotnet user-secrets set "ClientsPath" "<path/to/clients/apps>"
   ```

2. Run `dotnet run` as normal. The `web-frontend` resource starts with **explicit start** — open
   the Aspire dashboard and start it manually when you're ready.

In self-hosted mode the web frontend automatically uses the `build:bit:selfhost:watch` npm script
and its port is incremented by 1 (matching the server services).

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
```

In self-hosted mode:
- The SQL Server password comes from `dev:selfHostOverride:globalSettings:sqlServer:connectionString`
  in `dev/secrets.json` when it is set
- The database name changes from `vault_dev` to `vault_dev_self_host`
- `migrate-mssql-selfhost` (`dev/migrate.ps1 -selfhost`) starts instead of `migrate-mssql`
- Each service receives the `developSelfHosted=true` environment variable
- Each service's effective port becomes `BasePort + 1`
- The web frontend uses the `build:bit:selfhost:watch` npm script and its port is also incremented by 1

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
- `NgrokAuthToken` is sensitive — always store it with `dotnet user-secrets`, never in any
  `appsettings.*.json` file. Database passwords stay in `dev/secrets.json`, which is gitignored.
- User secrets are stored outside the repo in your OS profile, keyed by the `UserSecretsId` in
  `AppHost.csproj`, and are never tracked by git.
- `AppHost/appsettings.local.json` is gitignored and loaded last. Use it for non-secret overrides
  such as paths and ports; the bullet above still applies to it.

## Git Worktrees

Path-based settings resolve relative to the `AppHost` directory. If your worktree lives in
a different location than the main checkout, those paths won't resolve correctly. Use absolute
paths for any such setting.

User secrets are shared by every checkout of this repository, because they are keyed by the
`UserSecretsId` in `AppHost.csproj`. `AppHost/appsettings.local.json` belongs to one checkout: git
does not copy ignored files into a new worktree, so each worktree starts without one. Put values
that differ per worktree in that file:

- **`ClientsPath`** defaults to `../../clients/apps` — override if your worktree is not
  alongside the `clients` repo:

  ```json
  {
    "ClientsPath": "<absolute/path/to/clients/apps>"
  }
  ```

- **`AdditionalProjects:<name>:Path`** — use an absolute path when adding projects in a
  worktree:

  ```json
  {
    "AdditionalProjects": {
      "<name>": { "Path": "<absolute/path/to/Project.csproj>" }
    }
  }
  ```

A value that is the same for every checkout can stay in user secrets.

## Troubleshooting

| Symptom                          | Fix                                                                                                                                                                                                                                                                 |
|----------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Secrets not applied to services  | Re-run `setup-secrets` from the Aspire dashboard, or verify `dev/secrets.json` exists                                                                                                                                                                               |
| SQL Server container won't start | Confirm Docker Desktop is running and port 1433 is free                                                                                                                                                                                                             |
| Migrations fail immediately      | Ensure `pwsh` (PowerShell) is on your `$PATH`                                                                                                                                                                                                                       |
| Port conflicts on startup        | Set the conflicting `Services:<name>:BasePort` to a free port via user secrets                                                                                                                                                                                      |
| Services stuck waiting           | Check the `setup-secrets` logs, and that each connection string password in `dev/secrets.json` matches the password its reused volume was created with (see [Databases](#databases)). A resource that waits for an explicit-start resource waits until you start it |
| `migrate-mysql` cannot connect   | Check the port in `globalSettings:mySql:connectionString` in `dev/secrets.json` against `Databases:MySql:Port` or `Databases:MariaDb:Port`                                                                                                                          |
