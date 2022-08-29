using System.Data.SqlClient;
using System.Globalization;
using System.Net.Http.Json;
using Bit.Migrator;

namespace Bit.Setup;

public class Program
{
    private static Context _context;

    public static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

        _context = new Context
        {
            Args = args
        };
        ParseParameters();

        if (_context.Parameters.ContainsKey("q"))
        {
            _context.Quiet = _context.Parameters["q"] == "true" || _context.Parameters["q"] == "1";
        }
        if (_context.Parameters.ContainsKey("os"))
        {
            _context.HostOS = _context.Parameters["os"];
        }
        if (_context.Parameters.ContainsKey("corev"))
        {
            _context.CoreVersion = _context.Parameters["corev"];
        }
        if (_context.Parameters.ContainsKey("webv"))
        {
            _context.WebVersion = _context.Parameters["webv"];
        }
        if (_context.Parameters.ContainsKey("keyconnectorv"))
        {
            _context.KeyConnectorVersion = _context.Parameters["keyconnectorv"];
        }
        if (_context.Parameters.ContainsKey("stub"))
        {
            _context.Stub = _context.Parameters["stub"] == "true" ||
                _context.Parameters["stub"] == "1";
        }

        Helpers.WriteLine(_context);

        if (_context.Parameters.ContainsKey("install"))
        {
            Install();
        }
        else if (_context.Parameters.ContainsKey("update"))
        {
            Update();
        }
        else if (_context.Parameters.ContainsKey("printenv"))
        {
            PrintEnvironment();
        }
        else
        {
            Helpers.WriteLine(_context, "No top-level command detected. Exiting...");
        }
    }

    private static void Install()
    {
        if (_context.Parameters.ContainsKey("letsencrypt"))
        {
            _context.Config.SslManagedLetsEncrypt =
                _context.Parameters["letsencrypt"].ToLowerInvariant() == "y";
        }
        if (_context.Parameters.ContainsKey("domain"))
        {
            _context.Install.Domain = _context.Parameters["domain"].ToLowerInvariant();
        }
        if (_context.Parameters.ContainsKey("dbname"))
        {
            _context.Install.Database = _context.Parameters["dbname"];
        }

        if (_context.Stub)
        {
            _context.Install.InstallationId = Guid.Empty;
            _context.Install.InstallationKey = "SECRET_INSTALLATION_KEY";
        }
        else if (!ValidateInstallation())
        {
            return;
        }

        var certBuilder = new CertBuilder(_context);
        certBuilder.BuildForInstall();

        // Set the URL
        _context.Config.Url = string.Format("http{0}://{1}",
            _context.Config.Ssl ? "s" : string.Empty, _context.Install.Domain);

        var nginxBuilder = new NginxConfigBuilder(_context);
        nginxBuilder.BuildForInstaller();

        var environmentFileBuilder = new EnvironmentFileBuilder(_context);
        environmentFileBuilder.BuildForInstaller();

        var appIdBuilder = new AppIdBuilder(_context);
        appIdBuilder.Build();

        var dockerComposeBuilder = new DockerComposeBuilder(_context);
        dockerComposeBuilder.BuildForInstaller();

        _context.SaveConfiguration();

        Console.WriteLine("\nInstallation complete");

        Console.WriteLine("\nIf you need to make additional configuration changes, you can modify\n" +
            "the settings in `{0}` and then run:\n{1}",
            _context.HostOS == "win" ? ".\\bwdata\\config.yml" : "./bwdata/config.yml",
            _context.HostOS == "win" ? "`.\\bitwarden.ps1 -rebuild` or `.\\bitwarden.ps1 -update`" :
                "`./bitwarden.sh rebuild` or `./bitwarden.sh update`");

        Console.WriteLine("\nNext steps, run:");
        if (_context.HostOS == "win")
        {
            Console.WriteLine("`.\\bitwarden.ps1 -start`");
        }
        else
        {
            Console.WriteLine("`./bitwarden.sh start`");
        }
        Console.WriteLine(string.Empty);
    }

    private static void Update()
    {
        // This portion of code checks for multiple certs in the Identity.pfx PKCS12 bag.  If found, it generates
        // a new cert and bag to replace the old Identity.pfx.  This fixes an issue that came up as a result of
        // moving the project to .NET 5.
        _context.Install.IdentityCertPassword = Helpers.GetValueFromEnvFile("global", "globalSettings__identityServer__certificatePassword");
        var certCountString = Helpers.Exec("openssl pkcs12 -nokeys -info -in /bitwarden/identity/identity.pfx " +
            $"-passin pass:{_context.Install.IdentityCertPassword} 2> /dev/null | grep -c \"\\-----BEGIN CERTIFICATE----\"", true);
        if (int.TryParse(certCountString, out var certCount) && certCount > 1)
        {
            // Extract key from identity.pfx
            Helpers.Exec("openssl pkcs12 -in /bitwarden/identity/identity.pfx -nocerts -nodes -out identity.key " +
                $"-passin pass:{_context.Install.IdentityCertPassword} > /dev/null 2>&1");
            // Extract certificate from identity.pfx
            Helpers.Exec("openssl pkcs12 -in /bitwarden/identity/identity.pfx -clcerts -nokeys -out identity.crt " +
                $"-passin pass:{_context.Install.IdentityCertPassword} > /dev/null 2>&1");
            // Create new PKCS12 bag with certificate and key
            Helpers.Exec("openssl pkcs12 -export -out /bitwarden/identity/identity.pfx -inkey identity.key " +
                $"-in identity.crt -passout pass:{_context.Install.IdentityCertPassword} > /dev/null 2>&1");
        }

        if (_context.Parameters.ContainsKey("db"))
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
        _context.LoadConfiguration();
        if (!_context.PrintToScreen())
        {
            return;
        }
        Console.WriteLine("\nBitwarden is up and running!");
        Console.WriteLine("===================================================");
        Console.WriteLine("\nvisit {0}", _context.Config.Url);
        Console.Write("to update, run ");
        if (_context.HostOS == "win")
        {
            Console.Write("`.\\bitwarden.ps1 -updateself` and then `.\\bitwarden.ps1 -update`");
        }
        else
        {
            Console.Write("`./bitwarden.sh updateself` and then `./bitwarden.sh update`");
        }
        Console.WriteLine("\n");
    }

    private static void MigrateDatabase(int attempt = 1)
    {
        try
        {
            Helpers.WriteLine(_context, "Migrating database.");
            var vaultConnectionString = Helpers.GetValueFromEnvFile("global",
                "globalSettings__sqlServer__connectionString");
            var migrator = new DbMigrator(vaultConnectionString, null);
            var success = migrator.MigrateMsSqlDatabase(false);
            if (success)
            {
                Helpers.WriteLine(_context, "Migration successful.");
            }
            else
            {
                Helpers.WriteLine(_context, "Migration failed.");
            }
        }
        catch (SqlException e)
        {
            if (e.Message.Contains("Server is in script upgrade mode") && attempt < 10)
            {
                var nextAttempt = attempt + 1;
                Helpers.WriteLine(_context, "Database is in script upgrade mode. " +
                    "Trying again (attempt #{0})...", nextAttempt);
                System.Threading.Thread.Sleep(20000);
                MigrateDatabase(nextAttempt);
                return;
            }
            throw;
        }
    }

    private static bool ValidateInstallation()
    {
        var installationId = string.Empty;
        var installationKey = string.Empty;

        if (_context.Parameters.ContainsKey("install-id"))
        {
            installationId = _context.Parameters["install-id"].ToLowerInvariant();
        }
        else
        {
            installationId = Helpers.ReadInput("Enter your installation id (get at https://bitwarden.com/host)");
        }

        if (!Guid.TryParse(installationId.Trim(), out var installationidGuid))
        {
            Console.WriteLine("Invalid installation id.");
            return false;
        }

        if (_context.Parameters.ContainsKey("install-key"))
        {
            installationKey = _context.Parameters["install-key"];
        }
        else
        {
            installationKey = Helpers.ReadInput("Enter your installation key");
        }

        _context.Install.InstallationId = installationidGuid;
        _context.Install.InstallationKey = installationKey;

        try
        {
            var response = new HttpClient().GetAsync("https://api.bitwarden.com/installations/" +
                _context.Install.InstallationId).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine("Invalid installation id.");
                }
                else
                {
                    Console.WriteLine("Unable to validate installation id.");
                }

                return false;
            }

            var result = response.Content.ReadFromJsonAsync<InstallationValidationResponseModel>().GetAwaiter().GetResult();
            if (!result.Enabled)
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
        _context.LoadConfiguration();

        var environmentFileBuilder = new EnvironmentFileBuilder(_context);
        environmentFileBuilder.BuildForUpdater();

        var certBuilder = new CertBuilder(_context);
        certBuilder.BuildForUpdater();

        var nginxBuilder = new NginxConfigBuilder(_context);
        nginxBuilder.BuildForUpdater();

        var appIdBuilder = new AppIdBuilder(_context);
        appIdBuilder.Build();

        var dockerComposeBuilder = new DockerComposeBuilder(_context);
        dockerComposeBuilder.BuildForUpdater();

        _context.SaveConfiguration();
        Console.WriteLine(string.Empty);
    }

    private static void ParseParameters()
    {
        _context.Parameters = new Dictionary<string, string>();
        for (var i = 0; i < _context.Args.Length; i = i + 2)
        {
            if (!_context.Args[i].StartsWith("-"))
            {
                continue;
            }

            _context.Parameters.Add(_context.Args[i].Substring(1), _context.Args[i + 1]);
        }
    }

    class InstallationValidationResponseModel
    {
        public bool Enabled { get; init; }
    }
}
