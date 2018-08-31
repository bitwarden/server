using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bit.Setup
{
    public class Context
    {
        private const string ConfigPath = "/bitwarden/config.yml";

        public string[] Args { get; set; }
        public IDictionary<string, string> Parameters { get; set; }
        public string OutputDir { get; set; } = "/etc/bitwarden";
        public string HostOS { get; set; } = "win";
        public string CoreVersion { get; set; } = "latest";
        public string WebVersion { get; set; } = "latest";
        public Installation Install { get; set; } = new Installation();
        public Configuration Config { get; set; } = new Configuration();

        public void LoadConfiguration()
        {
            if(!File.Exists(ConfigPath))
            {
                Console.WriteLine("No existing `config.yml` detected. Let's generate one.");

                // Looks like updating from older version. Try to create config file.
                var url = Helpers.GetValueFronEnvFile("global", "globalSettings__baseServiceUri__vault");
                if(!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    Console.WriteLine("Unable to determine existing installation url.");
                    return;
                }
                Config.Url = url;

                var push = Helpers.GetValueFronEnvFile("global", "globalSettings__pushRelayBaseUri");
                Config.PushNotifications = push != "REPLACE";

                var composeFile = "/bitwarden/docker/docker-compose.yml";
                if(File.Exists(composeFile))
                {
                    var fileLines = File.ReadAllLines(composeFile);
                    foreach(var line in fileLines)
                    {
                        if(!line.StartsWith("# Parameter:"))
                        {
                            continue;
                        }

                        var paramParts = line.Split("=");
                        if(paramParts.Length < 2)
                        {
                            continue;
                        }

                        if(paramParts[0] == "# Parameter:MssqlDataDockerVolume" &&
                            bool.TryParse(paramParts[1], out var mssqlDataDockerVolume))
                        {
                            Config.DatabaseDockerVolume = mssqlDataDockerVolume;
                            continue;
                        }

                        if(paramParts[0] == "# Parameter:HttpPort" && int.TryParse(paramParts[1], out var httpPort))
                        {
                            Config.HttpPort = httpPort == 0 ? null : httpPort.ToString();
                            continue;
                        }

                        if(paramParts[0] == "# Parameter:HttpsPort" && int.TryParse(paramParts[1], out var httpsPort))
                        {
                            Config.HttpsPort = httpsPort == 0 ? null : httpsPort.ToString();
                            continue;
                        }
                    }
                }

                var nginxFile = "/bitwarden/nginx/default.conf";
                if(File.Exists(nginxFile))
                {
                    var confContent = File.ReadAllText(nginxFile);
                    var selfSigned = confContent.Contains("/etc/ssl/self/");
                    Config.Ssl = confContent.Contains("ssl http2;");
                    Config.SslManagedLetsEncrypt = !selfSigned && confContent.Contains("/etc/letsencrypt/live/");
                    var diffieHellman = confContent.Contains("/dhparam.pem;");
                    var trusted = confContent.Contains("ssl_trusted_certificate ");
                    if(Config.SslManagedLetsEncrypt)
                    {
                        Config.Ssl = true;
                    }
                    else if(Config.Ssl)
                    {
                        var sslPath = selfSigned ? $"/etc/ssl/self/{Config.Domain}" : $"/etc/ssl/{Config.Domain}";
                        Config.SslCertificatePath = string.Concat(sslPath, "/", "certificate.crt");
                        Config.SslKeyPath = string.Concat(sslPath, "/", "private.key");
                        if(trusted)
                        {
                            Config.SslCaPath = string.Concat(sslPath, "/", "ca.crt");
                        }
                        if(diffieHellman)
                        {
                            Config.SslDiffieHellmanPath = string.Concat(sslPath, "/", "dhparam.pem");
                        }
                    }
                }

                SaveConfiguration();
            }

            var configText = File.ReadAllText(ConfigPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new UnderscoredNamingConvention())
                .Build();
            Config = deserializer.Deserialize<Configuration>(configText);
        }

        public void SaveConfiguration()
        {
            if(Config == null)
            {
                throw new Exception("Config is null.");
            }
            var serializer = new SerializerBuilder()
                .EmitDefaults()
                .WithNamingConvention(new UnderscoredNamingConvention())
                .WithTypeInspector(inner => new CommentGatheringTypeInspector(inner))
                .WithEmissionPhaseObjectGraphVisitor(args => new CommentsObjectGraphVisitor(args.InnerVisitor))
                .Build();
            var yaml = serializer.Serialize(Config);
            Directory.CreateDirectory("/bitwarden/");
            using(var sw = File.CreateText(ConfigPath))
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
        }

        public class Configuration
        {
            [Description("Note: After making changes to this file you need to run the `rebuild` or `update`\n" +
                "command for them to be applied.\n\n" +
                "Full URL for accessing the installation from a browser. (Required)")]
            public string Url { get; set; } = "https://localhost";

            [Description("Auto-generate the `./docker/docker-compose.yml` config file.\n" +
                "WARNING: Disabling generated config files can break future updates. You will be\n" +
                "responsible for maintaining this config file.\n" +
                "Template: https://github.com/bitwarden/core/blob/master/util/Setup/Templates/DockerCompose.hbs")]
            public bool GenerateComposeConfig { get; set; } = true;

            [Description("Auto-generate the `./nginx/default.conf` file.\n" +
                "WARNING: Disabling generated config files can break future updates. You will be\n" +
                "responsible for maintaining this config file.\n" +
                "Template: https://github.com/bitwarden/core/blob/master/util/Setup/Templates/NginxConfig.hbs")]
            public bool GenerateNginxConfig { get; set; } = true;

            [Description("Docker compose file port mapping for HTTP. Leave empty to remove the port mapping.\n" +
                "Learn more: https://docs.docker.com/compose/compose-file/#ports")]
            public string HttpPort { get; set; } = "80";

            [Description("Docker compose file port mapping for HTTPS. Leave empty to remove the port mapping.\n" +
                "Learn more: https://docs.docker.com/compose/compose-file/#ports")]
            public string HttpsPort { get; set; } = "443";

            [Description("Docker compose file version. Leave empty for default.\n" +
                "Learn more: https://docs.docker.com/compose/compose-file/compose-versioning/")]
            public string ComposeVersion { get; set; }

            [Description("Configure Nginx for SSL.")]
            public bool Ssl { get; set; } = true;

            [Description("Installation uses a managed Let's Encrypt certificate.")]
            public bool SslManagedLetsEncrypt { get; set; }

            [Description("The actual certificate. (Required if using SSL without managed Let's Encrypt)\n" +
                "Note: Path uses the container's ssl directory. The `./ssl` host directory is mapped to\n" +
                "`/etc/ssl` within the container.")]
            public string SslCertificatePath { get; set; }

            [Description("The certificate's private key. (Required if using SSL without managed Let's Encrypt)\n" +
                "Note: Path uses the container's ssl directory. The `./ssl` host directory is mapped to\n" +
                "`/etc/ssl` within the container.")]
            public string SslKeyPath { get; set; }

            [Description("If the certificate is trusted by a CA, you should provide the CA's certificate.\n" +
                "Note: Path uses the container's ssl directory. The `./ssl` host directory is mapped to\n" +
                "`/etc/ssl` within the container.")]
            public string SslCaPath { get; set; }

            [Description("Diffie Hellman ephemeral parameters\n" +
                "Learn more: https://security.stackexchange.com/q/94390/79072\n" +
                "Note: Path uses the container's ssl directory. The `./ssl` host directory is mapped to\n" +
                "`/etc/ssl` within the container.")]
            public string SslDiffieHellmanPath { get; set; }

            [Description("Communicate with the Bitwarden push relay service (push.bitwarden.com) for mobile\n" +
                "app live sync.")]
            public bool PushNotifications { get; set; } = true;

            [Description("Use a docker volume instead of a host-mapped volume for the persisted database.\n" +
                "WARNING: Changing this value will cause you to lose access to the existing persisted\n" +
                "database.")]
            public bool DatabaseDockerVolume { get; set; }

            [YamlIgnore]
            public string Domain
            {
                get
                {
                    if(Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                    {
                        return uri.Host;
                    }
                    return null;
                }
            }
        }
    }
}
