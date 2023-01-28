using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bit.Setup;

public class Context
{
    private const string ConfigPath = "/bitwarden/config.yml";

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
