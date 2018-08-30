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
                    var selfSigned = false;
                    var diffieHellman = false;
                    var trusted = false;
                    var fileLines = File.ReadAllLines(nginxFile);
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

                        if(paramParts[0] == "# Parameter:Ssl" && bool.TryParse(paramParts[1], out var ssl))
                        {
                            Config.Ssl = ssl;
                            continue;
                        }

                        if(paramParts[0] == "# Parameter:LetsEncrypt" && bool.TryParse(paramParts[1], out var le))
                        {
                            Config.SslManagedLetsEncrypt = le;
                            continue;
                        }

                        if(paramParts[0] == "# Parameter:SelfSignedSsl" && bool.TryParse(paramParts[1], out var self))
                        {
                            selfSigned = self;
                            return;
                        }

                        if(paramParts[0] == "# Parameter:DiffieHellman" && bool.TryParse(paramParts[1], out var dh))
                        {
                            diffieHellman = dh;
                            return;
                        }

                        if(paramParts[0] == "# Parameter:Trusted" && bool.TryParse(paramParts[1], out var trust))
                        {
                            trusted = trust;
                            return;
                        }
                    }

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
            [Description("Full URL for accessing the installation from a browser. (Required)")]
            public string Url { get; set; } = "https://localhost";

            [Description("Auto-generate the `./docker/docker-compose.yml` config file.\n" +
                "WARNING: Disabling generated config files can break future updates. You will be responsible\n" +
                "for maintaining this config file.")]
            public bool GenerateComposeConfig { get; set; } = true;

            [Description("Auto-generate the `./nginx/default.conf` file.\n" +
                "WARNING: Disabling generated config files can break future updates. You will be responsible\n" +
                "for maintaining this config file.")]
            public bool GenerateNginxConfig { get; set; } = true;

            [Description("Compose file port mapping for HTTP. Leave empty for remove the port mapping.")]
            public string HttpPort { get; set; } = "80";

            [Description("Compose file port mapping for HTTPS. Leave empty for remove the port mapping.")]
            public string HttpsPort { get; set; } = "443";

            [Description("Set up the Nginx config file for SSL.")]
            public bool Ssl { get; set; } = true;

            [Description("Installation uses a managed Let's Encrypt certificate.")]
            public bool SslManagedLetsEncrypt { get; set; }

            [Description("The actual certificate. (Required if using SSL without managed Let's Encrypt)\n" +
                "Note: The `./ssl` directory is mapped to `/etc/ssl` within the container.")]
            public string SslCertificatePath { get; set; }

            [Description("The certificate's private key. (Required if using SSL without managed Let's Encrypt)\n" +
                "Note: The `./ssl` directory is mapped to `/etc/ssl` within the container.")]
            public string SslKeyPath { get; set; }

            [Description("If the certificate is trusted by a CA, you should provide the CA's certificate.\n" +
                "Note: The `./ssl` directory is mapped to `/etc/ssl` within the container.")]
            public string SslCaPath { get; set; }

            [Description("Diffie Hellman ephemeral parameters\n" +
                "Learn more: https://security.stackexchange.com/q/94390/79072\n" +
                "Note: The `./ssl` directory is mapped to `/etc/ssl` within the container.")]
            public string SslDiffieHellmanPath { get; set; }

            [Description("Communicate with the Bitwarden push relay service (push.bitwarden.com) for mobile app live sync.")]
            public bool PushNotifications { get; set; } = true;

            [Description("Use a docker volume instead of a host-mapped volume for the persisted database.\n" +
                "WARNING: Changing this value will cause you to lose access to the existing persisted database.")]
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
