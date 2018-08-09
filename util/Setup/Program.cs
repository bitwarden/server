using DbUp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Reflection;
using System.IO;

namespace Bit.Setup
{
    public class Program
    {
        private static string[] _args = null;
        private static IDictionary<string, string> _parameters = null;
        private static Guid? _installationId = null;
        private static string _installationKey = null;
        private static string _hostOs = "win";
        private static string _coreVersion = "latest";
        private static string _webVersion = "latest";

        public static void Main(string[] args)
        {
            Console.WriteLine();

            _args = args;
            _parameters = ParseParameters();
            if(_parameters.ContainsKey("os"))
            {
                _hostOs = _parameters["os"];
            }
            if(_parameters.ContainsKey("corev"))
            {
                _coreVersion = _parameters["corev"];
            }
            if(_parameters.ContainsKey("webv"))
            {
                _webVersion = _parameters["webv"];
            }

            if(_parameters.ContainsKey("install"))
            {
                Install();
            }
            else if(_parameters.ContainsKey("update"))
            {
                Update();
            }
            else if(_parameters.ContainsKey("printenv"))
            {
                PrintEnvironment();
            }
            else
            {
                Console.WriteLine("No top-level command detected. Exiting...");
            }
        }

        private static void Install()
        {
            var outputDir = _parameters.ContainsKey("out") ?
                _parameters["out"].ToLowerInvariant() : "/etc/bitwarden";
            var domain = _parameters.ContainsKey("domain") ?
                _parameters["domain"].ToLowerInvariant() : "localhost";
            var letsEncrypt = _parameters.ContainsKey("letsencrypt") ?
                _parameters["letsencrypt"].ToLowerInvariant() == "y" : false;

            if(!ValidateInstallation())
            {
                return;
            }

            var ssl = letsEncrypt;
            if(!letsEncrypt)
            {
                ssl = Helpers.ReadQuestion("Do you have a SSL certificate to use?");
                if(ssl)
                {
                    Directory.CreateDirectory($"/bitwarden/ssl/{domain}/");
                    var message = "Make sure 'certificate.crt' and 'private.key' are provided in the \n" +
                                  "appropriate directory before running 'start' (see docs for info).";
                    Helpers.ShowBanner("NOTE", message);
                }
            }

            var identityCertPassword = Helpers.SecureRandomString(32, alpha: true, numeric: true);
            var certBuilder = new CertBuilder(domain, identityCertPassword, letsEncrypt, ssl);
            var selfSignedSsl = certBuilder.BuildForInstall();
            ssl = certBuilder.Ssl; // Ssl prop can get flipped during the build

            var sslTrusted = letsEncrypt;
            var sslDiffieHellman = letsEncrypt;
            if(ssl && !selfSignedSsl && !letsEncrypt)
            {
                sslDiffieHellman = Helpers.ReadQuestion("Use Diffie Hellman ephemeral parameters for SSL " +
                    "(requires dhparam.pem, see docs)?");
                sslTrusted = Helpers.ReadQuestion("Is this a trusted SSL certificate (requires ca.crt, see docs)?");
            }

            if(!ssl)
            {
                var message = "You are not using a SSL certificate. Bitwarden requires HTTPS to operate. \n" +
                              "You must front your installation with a HTTPS proxy. The web vault (and \n" +
                              "other Bitwarden apps) will not work properly without HTTPS.";
                Helpers.ShowBanner("WARNING", message, ConsoleColor.Yellow);
            }
            else if(ssl && !sslTrusted)
            {
                var message = "You are using an untrusted SSL certificate. This certificate will not be \n" +
                              "trusted by Bitwarden client applications. You must add this certificate to \n" +
                              "the trusted store on each device or else you will receive errors when trying \n" +
                              "to connect to your installation.";
                Helpers.ShowBanner("WARNING", message, ConsoleColor.Yellow);
            }

            var url = $"https://{domain}";
            int httpPort = default(int), httpsPort = default(int);
            if(Helpers.ReadQuestion("Do you want to use the default ports for HTTP (80) and HTTPS (443)?"))
            {
                httpPort = 80;
                if(ssl)
                {
                    httpsPort = 443;
                }
            }
            else if(ssl)
            {
                httpsPort = 443;
                if(int.TryParse(Helpers.ReadInput("HTTPS port").Trim(), out httpsPort) && httpsPort != 443)
                {
                    url += (":" + httpsPort);
                }
                else
                {
                    Console.WriteLine("Using default port.");
                }
            }
            else
            {
                httpPort = 80;
                if(!int.TryParse(Helpers.ReadInput("HTTP port").Trim(), out httpPort) && httpPort != 80)
                {
                    Console.WriteLine("Using default port.");
                }
            }

            if(Helpers.ReadQuestion("Is your installation behind a reverse proxy?"))
            {
                if(Helpers.ReadQuestion("Do you use the default HTTPS port (443) on your reverse proxy?"))
                {
                    url = $"https://{domain}";
                }
                else
                {
                    if(int.TryParse(Helpers.ReadInput("Proxy HTTPS port").Trim(), out var httpsReversePort)
                        && httpsReversePort != 443)
                    {
                        url += (":" + httpsReversePort);
                    }
                    else
                    {
                        Console.WriteLine("Using default port.");
                        url = $"https://{domain}";
                    }
                }
            }
            else if(!ssl)
            {
                Console.WriteLine("ERROR: You must use a reverse proxy if not using SSL.");
                return;
            }

            var push = Helpers.ReadQuestion("Do you want to use push notifications?");

            var nginxBuilder = new NginxConfigBuilder(domain, url, ssl, selfSignedSsl, letsEncrypt,
                sslTrusted, sslDiffieHellman);
            nginxBuilder.BuildForInstaller();

            var environmentFileBuilder = new EnvironmentFileBuilder
            {
                DatabasePassword = Helpers.SecureRandomString(32),
                Domain = domain,
                IdentityCertPassword = identityCertPassword,
                InstallationId = _installationId,
                InstallationKey = _installationKey,
                OutputDirectory = outputDir,
                Push = push,
                Url = url
            };
            environmentFileBuilder.BuildForInstaller();

            var appIdBuilder = new AppIdBuilder(url);
            appIdBuilder.Build();

            var dockerComposeBuilder = new DockerComposeBuilder(_hostOs, _webVersion, _coreVersion);
            dockerComposeBuilder.BuildForInstaller(httpPort, httpsPort);
        }

        private static void Update()
        {
            if(_parameters.ContainsKey("db"))
            {
                MigrateDatabase();
            }
            else
            {
                RebuildConfigs();
            }
        }

        private static void PrintEnvironment()
        {
            var vaultUrl = Helpers.GetValueFronEnvFile("global", "globalSettings__baseServiceUri__vault");
            Console.WriteLine("\nBitwarden is up and running!");
            Console.WriteLine("===================================================");
            Console.WriteLine("\nvisit {0}", vaultUrl);
            Console.Write("to update, run ");
            if(_hostOs == "win")
            {
                Console.Write("'.\\bitwarden.ps1 -updateself' and then '.\\bitwarden.ps1 -update'");
            }
            else
            {
                Console.Write("'./bitwarden.sh updateself' and then './bitwarden.sh update'");
            }
            Console.WriteLine("\n");
        }

        private static void MigrateDatabase(int attempt = 1)
        {
            try
            {
                Console.WriteLine("Migrating database.");

                var dbPass = Helpers.GetValueFronEnvFile("mssql", "SA_PASSWORD");
                var masterConnectionString = Helpers.MakeSqlConnectionString(
                    "mssql", "master", "sa", dbPass ?? string.Empty);
                var vaultConnectionString = Helpers.MakeSqlConnectionString(
                    "mssql", "vault", "sa", dbPass ?? string.Empty);

                using(var connection = new SqlConnection(masterConnectionString))
                {
                    var command = new SqlCommand(
                        "IF ((SELECT COUNT(1) FROM sys.databases WHERE [name] = 'vault') = 0) " +
                        "CREATE DATABASE [vault];", connection);
                    command.Connection.Open();
                    command.ExecuteNonQuery();
                    command.CommandText = "IF ((SELECT DATABASEPROPERTYEX([name], 'IsAutoClose') " +
                        "FROM sys.databases WHERE [name] = 'vault') = 1) " +
                        "ALTER DATABASE [vault] SET AUTO_CLOSE OFF;";
                    command.ExecuteNonQuery();
                }

                var upgrader = DeployChanges.To
                    .SqlDatabase(vaultConnectionString)
                    .JournalToSqlTable("dbo", "Migration")
                    .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly(),
                        s => s.Contains($".DbScripts.") && !s.Contains(".Archive."))
                    .WithTransaction()
                    .WithExecutionTimeout(new TimeSpan(0, 5, 0))
                    .LogToConsole()
                    .Build();

                var result = upgrader.PerformUpgrade();
                if(result.Successful)
                {
                    Console.WriteLine("Migration successful.");
                }
                else
                {
                    Console.WriteLine("Migration failed.");
                }
            }
            catch(SqlException e)
            {
                if(e.Message.Contains("Server is in script upgrade mode") && attempt < 10)
                {
                    var nextAttempt = attempt + 1;
                    Console.WriteLine("Database is in script upgrade mode. " +
                        "Trying again (attempt #{0})...", nextAttempt);
                    System.Threading.Thread.Sleep(20000);
                    MigrateDatabase(nextAttempt);
                    return;
                }

                throw e;
            }
        }

        private static bool ValidateInstallation()
        {
            var installationId = Helpers.ReadInput("Enter your installation id (get at https://bitwarden.com/host)");
            if(!Guid.TryParse(installationId.Trim(), out var installationidGuid))
            {
                Console.WriteLine("Invalid installation id.");
                return false;
            }

            _installationId = installationidGuid;
            _installationKey = Helpers.ReadInput("Enter your installation key");

            try
            {
                var response = new HttpClient().GetAsync("https://api.bitwarden.com/installations/" + _installationId)
                    .GetAwaiter().GetResult();

                if(!response.IsSuccessStatusCode)
                {
                    if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("Invalid installation id.");
                    }
                    else
                    {
                        Console.WriteLine("Unable to validate installation id.");
                    }

                    return false;
                }

                var resultString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var result = JsonConvert.DeserializeObject<dynamic>(resultString);
                if(!(bool)result.Enabled)
                {
                    Console.WriteLine("Installation id has been disabled.");
                    return false;
                }

                return true;
            }
            catch
            {
                Console.WriteLine("Unable to validate installation id. Problem contacting Bitwarden server.");
                return false;
            }
        }

        private static void RebuildConfigs()
        {
            var environmentFileBuilder = new EnvironmentFileBuilder();
            environmentFileBuilder.BuildForUpdater();

            var url = Helpers.GetValueFronEnvFile("global", "globalSettings__baseServiceUri__vault");
            if(!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                Console.WriteLine("Unable to determine existing installation url.");
                return;
            }

            var domain = uri.Host;

            var nginxBuilder = new NginxConfigBuilder(domain, url);
            nginxBuilder.BuildForUpdater();

            var appIdBuilder = new AppIdBuilder(url);
            appIdBuilder.Build();

            var dockerComposeBuilder = new DockerComposeBuilder(_hostOs, _webVersion, _coreVersion);
            dockerComposeBuilder.BuildForUpdater();
        }

        private static IDictionary<string, string> ParseParameters()
        {
            var dict = new Dictionary<string, string>();
            for(var i = 0; i < _args.Length; i = i + 2)
            {
                if(!_args[i].StartsWith("-"))
                {
                    continue;
                }

                dict.Add(_args[i].Substring(1), _args[i + 1]);
            }

            return dict;
        }
    }
}
