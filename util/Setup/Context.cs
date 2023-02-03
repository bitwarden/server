using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bit.Setup;

public class Context
{
    private const string ConfigPath = "/bitwarden/config.yml";

    // These track of old CSP default values to correct.
    // Do not change these values.
    private const string Dec2020ContentSecurityPolicy = "default-src 'self'; style-src 'self' " +
        "'unsafe-inline'; img-src 'self' data: https://haveibeenpwned.com https://www.gravatar.com; " +
        "child-src 'self' https://*.duosecurity.com; frame-src 'self' https://*.duosecurity.com; " +
        "connect-src 'self' wss://{0} https://api.pwnedpasswords.com " +
        "https://twofactorauth.org; object-src 'self' blob:;";
    private const string Jan2021ContentSecurityPolicy = "default-src 'self'; style-src 'self' " +
        "'unsafe-inline'; img-src 'self' data: https://haveibeenpwned.com https://www.gravatar.com; " +
        "child-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "frame-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "connect-src 'self' wss://{0} https://api.pwnedpasswords.com " +
        "https://twofactorauth.org; object-src 'self' blob:;";
    private const string Feb2021ContentSecurityPolicy = "default-src 'self'; style-src 'self' " +
        "'unsafe-inline'; img-src 'self' data: https://haveibeenpwned.com https://www.gravatar.com; " +
        "child-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "frame-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "connect-src 'self' wss://{0} https://api.pwnedpasswords.com " +
        "https://2fa.directory; object-src 'self' blob:;";
    private const string Jan2023ContentSecurityPolicy = "default-src 'self'; style-src 'self' " +
        "'unsafe-inline'; img-src 'self' data: https://haveibeenpwned.com; " +
        "child-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "frame-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "connect-src 'self' wss://{0} https://api.pwnedpasswords.com " +
        "https://api.2fa.directory; object-src 'self' blob:;";

    private string[] _oldCspDefaults =
    {
        Dec2020ContentSecurityPolicy,
        Jan2021ContentSecurityPolicy,
        Feb2021ContentSecurityPolicy,
        Jan2023ContentSecurityPolicy
    };

    public string[] Args { get; set; }
    public bool Quiet { get; set; }
    public bool Stub { get; set; }
    public IDictionary<string, string> Parameters { get; set; }
    public string OutputDir { get; set; } = "/etc/bitwarden";
    public string HostOS { get; set; } = "win";
    public string CoreVersion { get; set; } = "latest";
    public string WebVersion { get; set; } = "latest";
    public string KeyConnectorVersion { get; set; } = "latest";
    public Installation Install { get; set; } = new Installation();
    public Configuration Config { get; set; } = new Configuration();

    public bool PrintToScreen()
    {
        return !Quiet || Parameters.ContainsKey("install");
    }

    public void LoadConfiguration()
    {
        if (!File.Exists(ConfigPath))
        {
            Helpers.WriteLine(this, "No existing `config.yml` detected. Let's generate one.");

            // Looks like updating from older version. Try to create config file.
            var url = Helpers.GetValueFromEnvFile("global", "globalSettings__baseServiceUri__vault");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Helpers.WriteLine(this, "Unable to determine existing installation url.");
                return;
            }
            Config.Url = url;

            var push = Helpers.GetValueFromEnvFile("global", "globalSettings__pushRelayBaseUri");
            Config.PushNotifications = push != "REPLACE";

            var composeFile = "/bitwarden/docker/docker-compose.yml";
            if (File.Exists(composeFile))
            {
                var fileLines = File.ReadAllLines(composeFile);
                foreach (var line in fileLines)
                {
                    if (!line.StartsWith("# Parameter:"))
                    {
                        continue;
                    }

                    var paramParts = line.Split("=");
                    if (paramParts.Length < 2)
                    {
                        continue;
                    }

                    if (paramParts[0] == "# Parameter:MssqlDataDockerVolume" &&
                        bool.TryParse(paramParts[1], out var mssqlDataDockerVolume))
                    {
                        Config.DatabaseDockerVolume = mssqlDataDockerVolume;
                        continue;
                    }

                    if (paramParts[0] == "# Parameter:HttpPort" && int.TryParse(paramParts[1], out var httpPort))
                    {
                        Config.HttpPort = httpPort == 0 ? null : httpPort.ToString();
                        continue;
                    }

                    if (paramParts[0] == "# Parameter:HttpsPort" && int.TryParse(paramParts[1], out var httpsPort))
                    {
                        Config.HttpsPort = httpsPort == 0 ? null : httpsPort.ToString();
                        continue;
                    }
                }
            }

            var nginxFile = "/bitwarden/nginx/default.conf";
            if (File.Exists(nginxFile))
            {
                var confContent = File.ReadAllText(nginxFile);
                var selfSigned = confContent.Contains("/etc/ssl/self/");
                Config.Ssl = confContent.Contains("ssl http2;");
                Config.SslManagedLetsEncrypt = !selfSigned && confContent.Contains("/etc/letsencrypt/live/");
                var diffieHellman = confContent.Contains("/dhparam.pem;");
                var trusted = confContent.Contains("ssl_trusted_certificate ");
                if (Config.SslManagedLetsEncrypt)
                {
                    Config.Ssl = true;
                }
                else if (Config.Ssl)
                {
                    var sslPath = selfSigned ? $"/etc/ssl/self/{Config.Domain}" : $"/etc/ssl/{Config.Domain}";
                    Config.SslCertificatePath = string.Concat(sslPath, "/", "certificate.crt");
                    Config.SslKeyPath = string.Concat(sslPath, "/", "private.key");
                    if (trusted)
                    {
                        Config.SslCaPath = string.Concat(sslPath, "/", "ca.crt");
                    }
                    if (diffieHellman)
                    {
                        Config.SslDiffieHellmanPath = string.Concat(sslPath, "/", "dhparam.pem");
                    }
                }
            }

            SaveConfiguration();
        }

        var configText = File.ReadAllText(ConfigPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        Config = deserializer.Deserialize<Configuration>(configText);

        // Fix old explicit config assignments of CSP which should be treated as a default value
        if (_oldCspDefaults.Any(c => c == Config.NginxHeaderContentSecurityPolicy))
        {
            Config.NginxHeaderContentSecurityPolicy = null;
            SaveConfiguration();
        }
    }

    public void SaveConfiguration()
    {
        if (Config == null)
        {
            throw new Exception("Config is null.");
        }
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
            .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
            .Build();
        var yaml = serializer.Serialize(Config);
        Directory.CreateDirectory("/bitwarden/");
        using (var sw = File.CreateText(ConfigPath))
        {
            sw.Write(yaml);
        }
    }

    public class Installation
    {
        public Guid InstallationId { get; set; }
        public string InstallationKey { get; set; }
        public bool DiffieHellman { get; set; }
        public bool Trusted { get; set; }
        public bool SelfSignedCert { get; set; }
        public string IdentityCertPassword { get; set; }
        public string Domain { get; set; }
        public string Database { get; set; }
    }
}
