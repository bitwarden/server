# Server Architecture

The Server is divided into a number of services. Each service is a Visual Studio project in the Server solution. These are:

* Admin
* Api
* Icons
* Identity
* Notifications
* SQL

Each service is built and run separately. The Bitwarden clients can use different servers for different services.

This means that you don't need to run all services locally for a development environment. You can run only those services that you intend to modify, and use Bitwarden.com or a self-hosted instance for all other services required.

By default some of the services depends on the Bitwarden licensed `CommCore`, however it can easily be disabled by including the `/p:DefineConstants="OSS"` as an argument to `dotnet`.

# Local Development Environment Setup

This guide will show you how to set up the Api, Identity and SQL projects for development. These are the minimum projects for any development work. You may need to set up additional projects depending on the changes you want to make.

We recommend using [Visual Studio](https://visualstudio.microsoft.com/vs/), and [PowerShell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.1) which is used for the helper scripts. 

## Docker containers

To simplify the setup process we provide a [Docker Compose](https://docs.docker.com/compose/) application model. This is split up into multiple service profiles to facilitate easily customization.

Some settings can be customized by modifying the `dev/.env` file, such as the `MSSQL_PASSWORD` which should be modified before starting the project.

```bash
# Copy the example environment file
cp ./.env.example ./.env

# We recommend running the following command when developing for self-hosted
docker compose --profile mssql --profile mail up

# We also provide a storage profile which uses Azurite to emulate some services used by the cloud instance
#  Usually only needed by internal Bitwarden developers
docker compose --profile cloud --profile mail up
```

### SQL Server

We recommend changing the `MSSQL_PASSWORD` variable in `dev/.env` to avoid exposing the sqlserver with a default password. **Note**: changing this after first running docker compose may require a re-creation of the storage volume. To do this, stop the running containers and run `docker volume rm bitwardenserver_mssql_dev_data`. (**Warning:** this will delete your development database.)

We provide a helper script which will create the development database `vault_dev` and also run all migrations. This command should be run after starting docker the first time, as well as after syncing against upstream and after creating a new migration.

```powershell
.\dev\migrate.ps1

# You can also re-run the last migration using
.\dev\migrate.ps1 -r
```

**Note:** If all or many migrations are skipped even though this is a new database, make sure that there is not a `last_migration` file located in `dev/.data/mssql`. If there is, remove it and run the helper script again. This can happen if you create the database, run migrations, then delete it.

### Azurite

[Azurite](https://github.com/Azure/Azurite) is a emulator for Azure Storage API and supports Blob, Queues and Table storage. We use it to avoid a hard dependency on online services for cloud development.

To bootstrap the local Azurite instance please run the following command:
```powershell
# This script requires the Az module, which can be installed using
Install-Module -Name Az -Scope CurrentUser -Repository PSGallery -Force

.\dev\setup_azurite.ps1
```

### Mailcatcher

Since the server uses emails for many user interactions a working SMTP server is a requirement, we provide a pre-setup instance of [MailCatcher](https://mailcatcher.me/) which exposes a web interface at http://localhost:1080.

## Certificates
In order to run Bitwarden, we require two certificates which for local development can be resolved by using self signed certificates.

### Windows

We provide a helper script which will generate and add the certificates to the users Certificate Store. After running the script it will output the thumbprints needed for the next step. The certificates can later be accessed using `certml.msc` under `Personal/Certificates`.

```powershell
.\create_certificates_windows.ps1

   PSParentPath: Microsoft.PowerShell.Security\Certificate::CurrentUser\My
Thumbprint                                Subject
----------                                -------
0BE8A0072214AB37C6928968752F698EEC3A68B5  CN=Bitwarden Identity Server Dev
C3A6CECAD3DB580F91A52FC9C767FE780300D8AB  CN=Bitwarden Data Protection Dev
```

### MacOS

We provide a helper script which will generate the certificates and add them to the keychain.

**Note:** You should update the Trust options for each certificate to `always trust` using *Keychain Access*.

```bash
./create_certificates_mac.sh

Certificate fingerprints:
Identity Server Dev: 0BE8A0072214AB37C6928968752F698EEC3A68B5
Data Protection Dev: C3A6CECAD3DB580F91A52FC9C767FE780300D8AB
```

## User Secrets
User secrets are a method for managing application settings on a per-developer basis. They are stored outside of the local git repository so that they are not pushed to remote.

User secrets override the settings in `appsettings.json` of each project. Your user secrets file should match the structure of the `appsettings.json` file for the settings you intend to override.

For more information, see: [Safe storage of app secrets in development in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-5.0).

### Automated Helper script

We provide a helper scripts which simplifies setting user secrets for all projects in the repository.

Start by copying the `secret.json.example` file to `secrets.json` and modify the existing settings and add any other required setting. Afterwards run the following command which will add the settings to each project in the bitwarden repository.

```powershell
.\setup_secrets.ps1

# The script also supports an optional flag which removes all existing settings before re-applying them
.\setup_secrets.ps1 -clear 1
```

### Manually creating and modifying

It is also possible to manually create and modify the user secrets using either the `dotnet` CLI or `Visual Studio` on Windows. For more details see [Appendix A](#user-secrets).

### Required User Secrets

**selfhosted**: It is highly recommended that you use the `selfHosted: true` setting when running a local development environment. This tells the system not to use cloud services, assuming that you are running your own local SQL instance. 

**sqlServer__connectionString**: this provides the information required for the Server to connect to the SQL instance. See the example connection string in `secrets.json.example`. You may need to change the default password in the connection string.

**licenseDirectory**: this must be set to avoid errors, but it can be set to an arbitrary empty folder.

**installation__key** and **installation__id**: request your own private Installation Id and Installation Key for self-hosting: https://bitwarden.com/host/.

## Running and Debugging
After you have completed the above steps, you should be ready to launch your development environment for the Api and Identity projects.

### Visual Studio

To debug:
* On Windows, right-click on each project > click **Debug** > click **Start New Instance**
* On MacOS, right-click each project > click **Start Debugging Project**

To run without debugging, open a terminal and navigate to the location of the .csproj file for that project (usually in `src/ProjectName`). Start the project with:

```bash
dotnet run
```

NOTE: check the output of the running project to find the port it is listening on. If this is different to the default in `appsettings.json`, you may need to update your user secrets to override this (typically the Api user secrets for the Identity URL).

### Rider
From within Rider, launch both the Api project and the Identity project by clicking the "play" button for each project separately.

### Testing your deployment
* To test the deployment of each project, navigate to the following pages in your browser. You should see server output and no errors:
  * Test the Api deployment: http://localhost:4000/alive
  * Test the Identity deployment: http://localhost:33656/.well-known/openid-configuration
* If your test was successful, you can connect a GUI client to the dev environment by following the instructions here: [Change your client application's environment](https://bitwarden.com/help/article/change-client-environment/). If you are following this guide, you should only set the API Server URL and Identity Server URL to localhost:port and leave all other fields blank.
* If you are using the CLI client, you will also need to set the Node environment variables for your self-signed certificates by following the instructions here: [The Bitwarden command-line tool (CLI) > Self-signed certificates](https://bitwarden.com/help/article/cli/#self-signed-certificates).

### Troubleshooting
* If you get a 404 error, the projects may be listening on a non-default port. Check the output of your running projects to check the port they are listening on.
* If you get an error while restoring the nuget packages like `error NU1403: Package content hash validation failed for ...` then these following commands should fix the problem:
  ```
  dotnet nuget locals all --clear
  git clean -xfd
  git rm **/packages.lock.json -f
  dotnet restore
  ```
  For more details read https://github.com/NuGet/Home/issues/7921#issuecomment-478152479


# <a name="user-secrets"></a>Appendix A (User Secrets)

### Editing user secrets - Visual Studio on Windows
Right-click on the project in the Solution Explorer and click **Manage User Secrets**.

### Editing user secrets - Visual Studio on macOS
Open a terminal and navigate to the project directory. Once there, initiate and create the blank user secrets file by running:

```bash
dotnet user-secrets init
```

Add a user secret by running:

```bash
dotnet user-secrets set "<key>" "<value>"
```

View currently set secrets by running:

```bash
dotnet user-secrets list
```

By default, user secret files are located in `~/.microsoft/usersecrets/<project name>/secrets.json`. After the file has been created, you can edit this directly with VSCode, which is much easier than using the command line tool.

### Editing user secrets - Rider
* Navigate to **Preferences -> Plugins** and Install .NET Core User Secrets
* Right click on the a project and click **Tools** > **Open project user secrets**
