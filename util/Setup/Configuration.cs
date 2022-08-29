using System.ComponentModel;
using YamlDotNet.Serialization;

namespace Bit.Setup;

public class Configuration
{
    [Description("Note: After making changes to this file you need to run the `rebuild` or `update`\n" +
        "command for them to be applied.\n\n" +

        "Full URL for accessing the installation from a browser. (Required)")]
    public string Url { get; set; } = "https://localhost";

    [Description("Auto-generate the `./docker/docker-compose.yml` config file.\n" +
        "WARNING: Disabling generated config files can break future updates. You will be\n" +
        "responsible for maintaining this config file.\n" +
        "Template: https://github.com/bitwarden/server/blob/master/util/Setup/Templates/DockerCompose.hbs")]
    public bool GenerateComposeConfig { get; set; } = true;

    [Description("Auto-generate the `./nginx/default.conf` file.\n" +
        "WARNING: Disabling generated config files can break future updates. You will be\n" +
        "responsible for maintaining this config file.\n" +
        "Template: https://github.com/bitwarden/server/blob/master/util/Setup/Templates/NginxConfig.hbs")]
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

    [Description("Configure Nginx for Captcha.")]
    public bool Captcha { get; set; } = false;

    [Description("Configure Nginx for SSL.")]
    public bool Ssl { get; set; } = true;

    [Description("SSL versions used by Nginx (ssl_protocols). Leave empty for recommended default.\n" +
        "Learn more: https://wiki.mozilla.org/Security/Server_Side_TLS")]
    public string SslVersions { get; set; }

    [Description("SSL ciphersuites used by Nginx (ssl_ciphers). Leave empty for recommended default.\n" +
        "Learn more: https://wiki.mozilla.org/Security/Server_Side_TLS")]
    public string SslCiphersuites { get; set; }

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

    [Description("Nginx Header Content-Security-Policy parameter\n" +
        "WARNING: Reconfiguring this parameter may break features. By changing this parameter\n" +
        "you become responsible for maintaining this value.")]
    public string NginxHeaderContentSecurityPolicy { get; set; } = "default-src 'self'; style-src 'self' " +
        "'unsafe-inline'; img-src 'self' data: https://haveibeenpwned.com https://www.gravatar.com; " +
        "child-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "frame-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "connect-src 'self' wss://{0} https://api.pwnedpasswords.com " +
        "https://2fa.directory; object-src 'self' blob:;";

    [Description("Communicate with the Bitwarden push relay service (push.bitwarden.com) for mobile\n" +
        "app live sync.")]
    public bool PushNotifications { get; set; } = true;

    [Description("Use a docker volume (`mssql_data`) instead of a host-mapped volume for the persisted " +
        "database.\n" +
        "WARNING: Changing this value will cause you to lose access to the existing persisted database.\n" +
        "Learn more: https://docs.docker.com/storage/volumes/")]
    public bool DatabaseDockerVolume { get; set; }

    [Description("Defines \"real\" IPs in nginx.conf. Useful for defining proxy servers that forward the \n" +
        "client IP address.\n" +
        "Learn more: https://nginx.org/en/docs/http/ngx_http_realip_module.html\n\n" +
        "Defined as a dictionary, e.g.:\n" +
        "real_ips: ['10.10.0.0/24', '172.16.0.0/16']")]
    public List<string> RealIps { get; set; }

    [Description("Enable Key Connector (https://bitwarden.com/help/article/deploy-key-connector)")]
    public bool EnableKeyConnector { get; set; } = false;

    [Description("Enable SCIM")]
    public bool EnableScim { get; set; } = false;

    [YamlIgnore]
    public string Domain
    {
        get
        {
            if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            {
                return uri.Host;
            }
            return null;
        }
    }
}
