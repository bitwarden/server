# How to Contribute

Contributions of all kinds are welcome!

Please visit our [Community Forums](https://community.bitwarden.com/) for general community discussion and the development roadmap.

Here is how you can get involved:

* **Request a new feature:** Go to the [Feature Requests category](https://community.bitwarden.com/c/feature-requests/) of the Community Forums. Please search existing feature requests before making a new one
  
* **Write code for a new feature:** Make a new post in the [Github Contributions category](https://community.bitwarden.com/c/github-contributions/) of the Community Forums. Include a description of your proposed contribution, screeshots, and links to any relevant feature requests. This helps get feedback from the community and Bitwarden team members before you start writing code
  
* **Report a bug or submit a bugfix:** Use Github issues and pull requests
  
* **Write documentation:** Submit a pull request to the [Bitwarden help repository](https://github.com/bitwarden/help)
  
* **Help other users:** Go to the [User-to-User Support category](https://community.bitwarden.com/c/support/) on the Community Forums

## Contributor Agreement

Please sign the [Contributor Agreement](https://cla-assistant.io/bitwarden/server) if you intend on contributing to any Github repository. Pull requests cannot be accepted and merged unless the author has signed the Contributor Agreement.

## Pull Request Guidelines

* commit any pull requests against the `master` branch
* include a link to your Community Forums post

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

# Local Development Environment Setup

This guide will show you how to set up the Api, Identity and SQL projects for development. These are the minimum projects for any development work. You may need to set up additional projects depending on the changes you want to make.

## SQL Server

There are 2 options for deploying your own SQL server.

### Without Docker

1. Install your own SQL server on localhost (e.g. SQL Express)
2. Right-click the SQL project in Visual Studio and click **Snapshot Project**. This will produce a .dacpac file containing the database schema
3. Use your preferred database management software (such as SQL Server Management Studio) to deploy a new database from the .dacpac file

### With Docker

1. Follow the [Installing and deploying > TL;DR](https://bitwarden.com/help/article/install-on-premise/#tldr) instructions to install and deploy a local Bitwarden Server using Docker. This will give you the entire Bitwarden Server (not just the SQL server), but it is the quickest and easiest method to get what you need.
2. Stop all containers
 
    Bash:
    ```bash
    ./bitwarden.sh stop
    ```

    Powershell:
    ```powershell
    .\bitwarden.ps1 -stop
    ```
4. Open a terminal with elevated privileges and navigate to your `bwdata` install folder
5. Run the SQL Docker container with these arguments:

    ```bash
    docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=<set an SQL password here>" -p 1433:1433 --name mssql-dev \
    --mount type=bind,source="$(pwd)"/mssql/data,target=/var/opt/mssql/data \
    --mount type=bind,source="$(pwd)"/logs/mssql,target=/var/opt/mssql/log \
    --mount type=bind,source="$(pwd)"/mssql/backups,target=/etc/bitwarden/mssql/backups bitwarden/mssql
    ```

Note: you will need the `SA_PASSWORD` you set here for the connection string in your user secrets (see below).
    
## User Secrets
User secrets are a method for managing application settings on a per-developer basis. They are stored outside of the local git repository so that they are not pushed to remote.

User secrets override the settings in `appsettings.json` of each project. Your user secrets file should match the structure of the `appsettings.json` file for the settings you intend to override.

For more information, see: [Safe storage of app secrets in development in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-3.1).

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

### Editing user secrets - Rider
* Navigate to **Preferences -> Plugins** and Install .NET Core User Secrets
* Right click on the a project and click **Tools** > **Open project user secrets**
 
## User Secrets - Certificates
Once you have your user secrets files set up, you'll need to generate 3 of your own certificates for use in local development.

This guide uses OpenSSL to generate the certificates. If you are using Windows, pre-compiled OpenSSL binaries are available via [Cygwin](https://www.cygwin.com/).

1. Open a terminal.
2. Create an Identity Server (Dev) certificate file (.crt) and key file (.key):
    ```bash
    openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity_server_dev.key -out identity_server_dev.crt -subj "/CN=Bitwarden Identity Server Dev" -days 3650
    ```
3. Create an Identity Server (Dev) .pfx file based on the certificate and key you just created. You will be prompted to enter a password - remember this because you’ll need it later:
    ```bash
    openssl pkcs12 -export -out identity_server_dev.pfx -inkey identity_server_dev.key -in identity_server_dev.crt -certfile identity_server_dev.crt
    ```
5. Create a Data Protection (Dev) certificate file (.crt) and key file (.key):
    ```bash
    openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout data_protection_dev.key -out data_protection_dev.crt -subj "/CN=Bitwarden Data Protection Dev" -days 3650
    ```
6. Create a Data Protection (Dev) .pfx file based on the certificate and key you just created. You will be prompted to enter a password - remember this because you’ll need it later:
    ```bash
    openssl pkcs12 -export -out data_protection_dev.pfx -inkey data_protection_dev.key -in data_protection_dev.crt -certfile data_protection_dev.crt
    ```
8. Install the .pfx files by double-clicking on them and entering the password when prompted. 
   * On Windows, this will add them to your certificate stores. You should add them to the "Trusted Root Certificate Authorities" store. 
   * On MacOS, this will add them to your keychain. You should update the Trust options for each certificate to `always trust`.
9.  Get the SHA1 thumbprint for the Identity and Data Protection certificates
    * On Windows
      * press Windows key + R to open the Run prompt
      * type "certmgr.msc" and press enter. This will open the system tool used to manage user certificates
      * find the "Bitwarden Data Protection Dev" and "Bitwarden Identity Server Dev" certificates in the Trusted Root Certificate Authorities > Certificates folder
      * double click on the certificate
      * click the "Details" tab and find the "Thumbprint" field in the list of properties.
    * On MacOS
      * press Command + Spacebar to open the Spotlight search
      * type "keychain access" and press enter
      * find the "Bitwarden Data Protection Dev" and "Bitwarden Identity Server Dev" certificates
      * select each certificate and click the "i" (information) button
      * find the SHA-1 fingerprint in the list of properties
10.  Add the SHA1 thumbprints of both certificates to your user secrets for the Api and Identity projects. (See the example user secrets file below.)

## User Secrets - Other

**selfhosted**: It is highly recommended that you use the `selfHosted: true` setting when running a local development environment. This tells the system not to use cloud services, assuming that you are running your own local SQL instance. 

Alternatively, there are emulators that allow you to run local dev instances of various Azure and/or AWS services (e.g. local-stack), or you can use your own Azure accounts for provisioning the necessary services and set the connection strings accordingly. These are outside the scope of this guide.

**sqlServer__connectionString**: this provides the information required for the Server to connect to the SQL instance. See the example connection string below.

**licenseDirectory**: this must be set to avoid errors, but it can be set to an aribtrary empty folder.

**installation__key** and **installation__id**: request your own private Installation Id and Installation Key for self-hosting: https://bitwarden.com/host/.

## Example User Secrets file

This is an example user secrets file for both the Api and Identity projects.

```json
{
  "globalSettings": {
    "selfHosted": true,
    "identityServer": {
      "certificateThumbprint": "<your Identity certificate thumbprint>"
    },
    "dataProtection": {
      "certificateThumbprint": "<your Data Protection certificate thumbprint>"
    },
    "installation": {
      "id": "<your Installation Id>",
      "key": "<your Installation Key>"
    },
    "licenseDirectory": "<full path to licence directory>",
    "sqlServer": {
      "connectionString": "Data Source=localhost,1433;Initial Catalog=vault;Persist Security Info=False;User ID=sa;Password=<your SQL password>;MultipleActiveResultSets=False;Connect Timeout=30;Encrypt=True;TrustServerCertificate=True"
    }
  }
}
```

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