# Bitwarden Server Development with VS Code Dev Containers

This guide helps you set up the Bitwarden Server development environment using VS Code Dev Containers. For more information, please refer to the [Server Setup Guide](https://contributing.bitwarden.com/getting-started/server/guide).

## Prerequisites

- [Visual Studio Code](https://code.visualstudio.com/)
- [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- **Disable VPN**: If you use a VPN (e.g., Zscaler, Cloudflare WARP), temporarily disable it during container build as it can interfere with Docker's network access (the root cert is not trusted)

## Quick Start

### 1. Configure Environment Variables

To avoid interactive prompts during container setup, create or update `dev/.env` with the following values:

```bash
# Copy the .env example file if it doesn't exist
cp dev/.env.example dev/.env
```

Add these configuration variables to your `dev/.env` file:

```bash
SETUP_SECRETS_JSON=yes
SETUP_AZURITE=no
RUN_MSSQL_MIGRATIONS=no
DEV_CERT_PASSWORD=*** # If you work at bitwarden, download the dev.pfx cert from the organisation vault, as described below
INSTALL_STRIPE_CLI=no
```

```bash
# Copy the secrets.json example file if it doesn't exist
cp dev/secrets.json.example dev/secrets.json
```

**Important:** In `secrets.json` you'll need to configure:
- Set a complex password for `MSSQL_SA_PASSWORD` (must follow [SQL Server password policy](https://docs.microsoft.com/en-us/sql/relational-databases/security/password-policy?view=sql-server-ver15) - basically: 8+ chars, uppercase, lowercase, digits, symbols)
- Set the `globalSettings:installation:id` in your secrets.json file, it needs to be a valid guid, e.g. `00000000-0000-0000-0000-000000000000`.

### 2. Choose Your Development Configuration

#### For Internal Bitwarden Developers
- Open the repository in VS Code
- When prompted, select "Reopen in Container" or use Command Palette: `> Dev Containers: Reopen in Container`
- Choose the **"Bitwarden Dev"** configuration

This configuration is best fitted for Bitwarden employees.

#### For Community Contributors
- Open the repository in VS Code
- When prompted, select "Reopen in Container" or use Command Palette: `Dev Containers: Reopen in Container`
- Choose the **"Bitwarden Community Dev"** configuration

This configuration is best fitted for community contributions.

### 3. Container Setup Process

The dev container will automatically:

1. **Build the container** with all necessary dependencies
2. **Start required services**:
   - SQL Server (port 1433)
   - Mail Catcher (port 1080)
   - PostgreSQL (port 5432) 
   - MySQL (port 3306)
   - Azurite storage services (ports 10000-10002)
3. **Run the post-creation script** which will prompt for (can be automated with values in the `.env`):
   - Secrets.json configuration (copies values from the `secrets.json` to each project)
   - Database migration execution
   - Certificate setup
   - Optional services (Azurite, Stripe CLI)

## Other

### Bitwarden Internal Development Certificate
Place the `dev.pfx` file from the internal shared collection in the `dev/` directory, or set the `DEV_CERT_CONTENTS` environment variable with the base64-encoded certificate content.

### Container Won't Start
- Ensure Docker Desktop is running
- **Disable VPN temporarily** - VPNs like Zscaler or Cloudflare WARP can block Docker's network access during build
- Verify your `dev/.env` file has valid configuration values

### Database Connection Issues  
- Confirm your `MSSQL_SA_PASSWORD` meets complexity requirements
- Check that the SQL Server container started successfully
- Verify the connection string in your secrets.json matches your password

### Certificate Issues
- Ensure the `dev.pfx` file is present in the `dev/` directory
- Verify you have the correct certificate password
- Check that the dotnet certificate tool installed successfully

For additional help, refer to the [Server Setup Guide](https://contributing.bitwarden.com/getting-started/server/guide) in the Contributing Documentation.