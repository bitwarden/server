# Contributing: Local Dev Environment Setup
For local development and running the Server (Api, Identity, etc.) you'll need to setup a few things in order to make that work, connect to your own local SQL instance (docker image), as well as have appropriate certificates available for performing encryption, signing, data protection, etc.

## User Secrets
You will need to manage user secrets for each site project in the Server solution. This includes predominately the Api and Identity projects, although there are other, lessor used ones that may also need it depending on what you're doing, but these 2 set the foundation.

### Self Hosted vs. Not
It is highly recommended that you use the `selfHosted: true` setting when running locally so the system will not attempt to use cloud services without a valid connection string. Otherwise there are emulators that allow you to run local dev instances of various Azure and/or AWS services such as local-stack, etc. or you can use your own Azure accounts for provisioning the necessary services and set the connection strings accordingly.

### Visual Studio on Windows
You can simply right-click on the Project and "View User Secrets" (or something like that), easy peasy.

### Visual Studio on macOS
You must open a terminal and go to each project directory. Once there you initiate and create the blank user secrets file by running,

```bash
dotnet user-secrets init
```

You can then view/list any secrets you have set by running, `dotnet user-secrets list`. For more info see: [Safe storage of app secrets in development in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-3.1&tabs=linux)

### Rider
* Navigate to **Preferences -> Plugins** and Install .NET Core User Secrets
* You can now right click on the a project (ex: Api), go to 'tools' and 'Open project user secrets'

## User Secrets - Certificates
Once you have your user secrets files setup, you'll need to generate 3 of your own certificates for use in local development.

### Generate Certificates
1. Within a terminal, navigate to ~/Desktop
2. Run the following command to create an Identity Server (Dev) certificate + key (separate)
```bash
openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity_server_dev.key -out identity_server_dev.crt -subj "/CN=Bitwarden Identity Server Dev" -days 3650
```
3. Run the following command to create an Identity Server (Dev) certificate + key pfx file based on the previously created certificate + key
4. You will be prompted to enter a password - remember this because you’ll need it for your keychain
```bash
openssl pkcs12 -export -out identity_server_dev.pfx -inkey identity_server_dev.key -in identity_server_dev.crt -certfile identity_server_dev.crt
```
5. Run the following command to create a Data Protection (Dev) certificate + key (separate)
```bash
openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout data_protection_dev.key -out data_protection_dev.crt -subj "/CN=Bitwarden Data Protection Dev" -days 3650
```
6. Once created, do the same process for a Data Protection (Dev) certificate + key pfx file
7. Again, create a password so you can save the files in your keychain
```bash
openssl pkcs12 -export -out data_protection_dev.pfx -inkey data_protection_dev.key -in data_protection_dev.crt -certfile data_protection_dev.crt
```
8. Add all of the generated pfx files to your login keychain by double-clicking on the files
   1. You’ll need to enter the respective generated password for each
9. Update the Trust options for each certificate to `always trust` - don’t worry, this is secure…. :)
10. Get the SHA 1 thumbprint for the Identity and Data Protection certificates and them to your Api User Secrets file
11. Copy + Paste the entire file into the Identity secrets
    1. Access the Identity user secrets the same way Api user

## Running and Debugging
Launching the entire bitwarden/server solution for use with the bitwarden/web and other client projects.

### Prerequisites
* Docker MSSQL instance running (and accessible localhost)
* An ADS connection to the `vault_dev` database is running
* [Node.js](https://nodejs.org/en/) is installed with updated PATH variables for launch
* User secrets are configured, password updated to MSSQL database

### Visual Studio for macOS
You can right click on each project, **Identity** and then **Api**, in order, and click _Start Debugging Project_.

You can also, alternatively if you only need them running locally w/o debugging, open a terminal and navigate to each respective .csproj location and type,

```bash
dotnet run
```

NOTE: when doing this the port number on the URL may be different and you'll need to update any client configuration(s) and/or user secrets accordingly (typically the Api user secrets for the Identity URL).

### Rider
* From within Rider, launch both the Api project and the Identity project
    * This is done by hitting the play button for each project separately

### Notes
* Don’t be alarmed when the default localhost:port for both projects opens and it shows a 404 error
    * Test the Api deployment: http://localhost:5000/alive
    * Test the Identity deployment: http://localhost:33657/.well-known/openid-configuration
    * You should not see any errors but rather responses on the webpage(s)
