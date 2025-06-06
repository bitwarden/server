﻿using System.Globalization;
using System.Net.Http.Json;
using Bit.Migrator;
using Bit.Setup.Enums;

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

        if (_context.Parameters.TryGetValue("q", out var q))
        {
            _context.Quiet = q == "true" || q == "1";
        }
        if (_context.Parameters.TryGetValue("os", out var os))
        {
            _context.HostOS = os;
        }
        if (_context.Parameters.TryGetValue("corev", out var coreVersion))
        {
            _context.CoreVersion = coreVersion;
        }
        if (_context.Parameters.TryGetValue("webv", out var webVersion))
        {
            _context.WebVersion = webVersion;
        }
        if (_context.Parameters.TryGetValue("keyconnectorv", out var keyConnectorVersion))
        {
            _context.KeyConnectorVersion = keyConnectorVersion;
        }
        if (_context.Parameters.TryGetValue("stub", out var stub))
        {
            _context.Stub = stub == "true" || stub == "1";
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
        if (_context.Parameters.TryGetValue("letsencrypt", out var sslManagedLetsEncrypt))
        {
            _context.Config.SslManagedLetsEncrypt =
                sslManagedLetsEncrypt.ToLowerInvariant() == "y";
        }
        if (_context.Parameters.TryGetValue("domain", out var domain))
        {
            _context.Install.Domain = domain.ToLowerInvariant();
        }
        if (_context.Parameters.TryGetValue("dbname", out var database))
        {
            _context.Install.Database = database;
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
            PrepareAndMigrateDatabase();
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

    private static void PrepareAndMigrateDatabase()
    {
        var vaultConnectionString = Helpers.GetValueFromEnvFile("global",
            "globalSettings__sqlServer__connectionString");
        var migrator = new DbMigrator(vaultConnectionString);

        var enableLogging = false;

        // execute all general migration scripts (will detect those not yet applied)
        migrator.MigrateMsSqlDatabaseWithRetries(enableLogging);

        // execute explicit transition migration scripts, per EDD
        migrator.MigrateMsSqlDatabaseWithRetries(enableLogging, true, MigratorConstants.TransitionMigrationsFolderName);
    }

    private static bool ValidateInstallation()
    {
        var installationId = string.Empty;
        var installationKey = string.Empty;
        CloudRegion cloudRegion;

        if (_context.Parameters.ContainsKey("install-id"))
        {
            installationId = _context.Parameters["install-id"].ToLowerInvariant();
        }
        else
        {
            var prompt = "Enter your installation id (get at https://bitwarden.com/host)";
            installationId = Helpers.ReadInput(prompt);
            while (string.IsNullOrEmpty(installationId))
            {
                Helpers.WriteError("Invalid input for installation id. Please try again.");
                installationId = Helpers.ReadInput(prompt);
            }
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
            var prompt = "Enter your installation key";
            installationKey = Helpers.ReadInput(prompt);
            while (string.IsNullOrEmpty(installationKey))
            {
                Helpers.WriteError("Invalid input for installation key. Please try again.");
                installationKey = Helpers.ReadInput(prompt);
            }
        }

        if (_context.Parameters.ContainsKey("cloud-region"))
        {
            Enum.TryParse(_context.Parameters["cloud-region"], out cloudRegion);
        }
        else
        {
            var prompt = "Enter your region (US/EU) [US]";
            var region = Helpers.ReadInput(prompt);
            if (string.IsNullOrEmpty(region)) region = "US";

            while (!Enum.TryParse(region, out cloudRegion))
            {
                Helpers.WriteError("Invalid input for region. Please try again.");
                region = Helpers.ReadInput(prompt);
                if (string.IsNullOrEmpty(region)) region = "US";
            }
        }

        _context.Install.InstallationId = installationidGuid;
        _context.Install.InstallationKey = installationKey;
        _context.Install.CloudRegion = cloudRegion;

        try
        {
            string url;
            switch (cloudRegion)
            {
                case CloudRegion.EU:
                    url = "https://api.bitwarden.eu/installations/";
                    break;
                case CloudRegion.US:
                default:
                    url = "https://api.bitwarden.com/installations/";
                    break;
            }

            string installationUrl = Environment.GetEnvironmentVariable("BW_INSTALLATION_URL");
            if (!string.IsNullOrEmpty(installationUrl))
            {
                url = $"{installationUrl}/installations/";
            }

            var response = new HttpClient().GetAsync(url + _context.Install.InstallationId).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"Invalid installation id for {cloudRegion.ToString()} region.");
                }
                else
                {
                    Console.WriteLine($"Unable to validate installation id for {cloudRegion.ToString()} region.");
                }

                return false;
            }

            var result = response.Content.ReadFromJsonAsync<InstallationValidationResponseModel>().GetAwaiter().GetResult();
            if (!result.Enabled)
            {
                Console.WriteLine($"Installation id has been disabled in the {cloudRegion.ToString()} region.");
                return false;
            }

            return true;
        }
        catch
        {
            Console.WriteLine($"Unable to validate installation id. Problem contacting Bitwarden {cloudRegion.ToString()} server.");
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
