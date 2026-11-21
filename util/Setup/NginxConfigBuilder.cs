// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Globalization;
using System.Text.RegularExpressions;

namespace Bit.Setup;

public class NginxConfigBuilder
{
    private string ConfFile => $"{_context.App.RootDirectory}/nginx/default.conf";

    private const string DefaultContentSecurityPolicy = "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https://haveibeenpwned.com; " +
        "child-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "frame-src 'self' https://*.duosecurity.com https://*.duofederal.com; " +
        "connect-src 'self' wss://{0} https://api.pwnedpasswords.com " +
        "https://api.2fa.directory; object-src 'self' blob:;";

    private readonly Context _context;

    public NginxConfigBuilder(Context context)
    {
        _context = context;
    }

    public void BuildForInstaller()
    {
        var model = new TemplateModel(_context);
        if (model.Ssl && !_context.Config.SslManagedLetsEncrypt)
        {
            var sslPath = _context.Install.SelfSignedCert ?
                $"/etc/ssl/self/{model.Domain}" : $"/etc/ssl/{model.Domain}";
            _context.Config.SslCertificatePath = model.CertificatePath =
                string.Concat(sslPath, "/", "certificate.crt");
            _context.Config.SslKeyPath = model.KeyPath =
                string.Concat(sslPath, "/", "private.key");
            if (_context.Install.Trusted)
            {
                _context.Config.SslCaPath = model.CaPath =
                    string.Concat(sslPath, "/", "ca.crt");
            }
            if (_context.Install.DiffieHellman)
            {
                _context.Config.SslDiffieHellmanPath = model.DiffieHellmanPath =
                    string.Concat(sslPath, "/", "dhparam.pem");
            }
        }
        Build(model);
    }

    public void BuildForUpdater()
    {
        var model = new TemplateModel(_context);
        Build(model);
    }

    private void Build(TemplateModel model)
    {
        Directory.CreateDirectory($"{_context.App.RootDirectory}/nginx/");
        Helpers.WriteLine(_context, "Building nginx config.");
        if (!_context.Config.GenerateNginxConfig)
        {
            Helpers.WriteLine(_context, "...skipped");
            return;
        }

        var template = Helpers.ReadTemplate("NginxConfig");
        using (var sw = File.CreateText(ConfFile))
        {
            sw.WriteLine(template(model));
        }
    }

    public class TemplateModel
    {
        public TemplateModel() { }

        public TemplateModel(Context context)
        {
            Ssl = context.Config.Ssl;
            EnableKeyConnector = context.Config.EnableKeyConnector;
            EnableScim = context.Config.EnableScim;
            Domain = context.Config.Domain;
            Url = context.Config.Url;
            RealIps = context.Config.RealIps;
            var csp = DefaultContentSecurityPolicy;
            if (!string.IsNullOrWhiteSpace(context.Config.NginxHeaderContentSecurityPolicy))
            {
                csp = context.Config.NginxHeaderContentSecurityPolicy;
            }
            ContentSecurityPolicy = string.Format(CultureInfo.InvariantCulture, csp, Domain);
            foreach (var blobOrigin in GetSendAndAttachmentBlobOrigins(context))
            {
                ContentSecurityPolicy = AppendConnectSrcOrigin(ContentSecurityPolicy, blobOrigin);
            }

            if (Ssl)
            {
                if (context.Config.SslManagedLetsEncrypt)
                {
                    var sslPath = $"/etc/letsencrypt/live/{Domain}";
                    CertificatePath = CaPath = string.Concat(sslPath, "/", "fullchain.pem");
                    KeyPath = string.Concat(sslPath, "/", "privkey.pem");
                    DiffieHellmanPath = string.Concat(sslPath, "/", "dhparam.pem");
                }
                else
                {
                    CertificatePath = context.Config.SslCertificatePath;
                    KeyPath = context.Config.SslKeyPath;
                    CaPath = context.Config.SslCaPath;
                    DiffieHellmanPath = context.Config.SslDiffieHellmanPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(context.Config.SslCiphersuites))
            {
                SslCiphers = context.Config.SslCiphersuites;
            }
            else
            {
                // Cipher list is Mozilla's "intermediate" list, See:
                // https://mozilla.github.io/server-side-tls/ssl-config-generator/
                SslCiphers = "ECDHE-ECDSA-AES128-GCM-SHA256:ECDHE-RSA-AES128-GCM-SHA256:" +
                    "ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:ECDHE-ECDSA-CHACHA20-POLY1305:" +
                    "ECDHE-RSA-CHACHA20-POLY1305:DHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384:" +
                    "DHE-RSA-CHACHA20-POLY1305;";
            }

            if (!string.IsNullOrWhiteSpace(context.Config.SslVersions))
            {
                SslProtocols = context.Config.SslVersions;
            }
            else
            {
                // Also based on Mozilla's Intermediate list
                SslProtocols = "TLSv1.2 TLSv1.3";
            }
            if (!string.IsNullOrWhiteSpace(context.Config.SslCurves))
            {
                SslCurves = context.Config.SslCurves;
            }
            else
            {
                // Also based on Mozilla's Intermediate list with one addition, the X25519MLKEM768 curve
                // for post quantum cryptography, X25519MLKEM768 has been adopted by most browsers at this
                // time. See https://blog.cloudflare.com/pq-2025/ for an in depth explanation.
                SslCurves = "X25519:X25519MLKEM768:prime256v1:secp384r1";
            }
        }

        public bool Ssl { get; set; }
        public bool EnableKeyConnector { get; set; }
        public bool EnableScim { get; set; }
        public string Domain { get; set; }
        public string Url { get; set; }
        public string CertificatePath { get; set; }
        public string KeyPath { get; set; }
        public string CaPath { get; set; }
        public string DiffieHellmanPath { get; set; }
        public string SslCiphers { get; set; }
        public string SslProtocols { get; set; }
        public string SslCurves { get; set; }
        public string ContentSecurityPolicy { get; set; }
        public List<string> RealIps { get; set; }
    }

    internal static bool TryGetSendBlobOrigin(Context context, out string origin)
    {
        origin = null;
        var connectionStringKey = "globalSettings__send__connectionString";
        var connectionString = GetConnectionString(context, connectionStringKey);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        if (TryGetBlobOrigin(connectionString, out origin))
        {
            return true;
        }

        Helpers.WriteLine(context,
            $"Invalid {connectionStringKey}: unable to determine a valid Azure Blob Storage endpoint.");
        return false;
    }

    private static IEnumerable<string> GetSendAndAttachmentBlobOrigins(Context context)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var connectionStringKey in new[]
        {
            "globalSettings__send__connectionString",
            "globalSettings__attachment__connectionString",
        })
        {
            var connectionString = GetConnectionString(context, connectionStringKey);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                continue;
            }

            if (TryGetBlobOrigin(connectionString, out var origin) &&
                origins.Add(origin))
            {
                yield return origin;
            }
            else if (string.IsNullOrEmpty(origin))
            {
                Helpers.WriteLine(context,
                    $"Invalid {connectionStringKey}: unable to determine a valid Azure Blob Storage endpoint.");
            }
        }
    }

    private static string GetConnectionString(Context context, string connectionStringKey)
    {
        var connectionString = Environment.GetEnvironmentVariable(connectionStringKey);
        return string.IsNullOrWhiteSpace(connectionString)
            ? Helpers.GetValueFromEnvFile(context.App, "global", connectionStringKey)
            : connectionString;
    }

    private static bool TryGetBlobOrigin(string connectionString, out string origin)
    {
        origin = null;
        var values = ParseConnectionString(connectionString);
        if (values.TryGetValue("BlobEndpoint", out var blobEndpoint))
        {
            return TryGetOrigin(blobEndpoint, out origin);
        }

        if (!values.TryGetValue("AccountName", out var accountName) ||
            !TryGetAccountName(accountName, out accountName))
        {
            return false;
        }

        var protocol = "https";
        if (values.TryGetValue("DefaultEndpointsProtocol", out var configuredProtocol) &&
            !string.IsNullOrWhiteSpace(configuredProtocol))
        {
            protocol = configuredProtocol.Trim().ToLowerInvariant();
        }

        if (protocol != "http" && protocol != "https")
        {
            return false;
        }

        if (!values.TryGetValue("EndpointSuffix", out var endpointSuffix) ||
            !TryGetEndpointSuffix(endpointSuffix, out endpointSuffix))
        {
            return false;
        }

        return TryGetOrigin($"{protocol}://{accountName}.blob.{endpointSuffix}", out origin);

    }

    internal static string AppendConnectSrcOrigin(string contentSecurityPolicy, string origin)
    {
        var pattern = @"(?<prefix>(^|;\s*))(?<directive>connect-src)(?<separator>\s+)(?<sources>[^;]*)" +
            @"(?<tail>(?=;|$))";
        var match = Regex.Match(contentSecurityPolicy, pattern, RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            var prefix = contentSecurityPolicy.TrimEnd();
            if (prefix.Length > 0 && !prefix.EndsWith(";"))
            {
                prefix += ";";
            }
            return prefix.Length == 0
                ? $"connect-src {origin};"
                : $"{prefix} connect-src {origin};";
        }

        var sources = match.Groups["sources"].Value;
        var sourceList = sources.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (sourceList.Any(source => string.Equals(source, origin, StringComparison.OrdinalIgnoreCase)))
        {
            return contentSecurityPolicy;
        }

        var suffix = sources.Length == 0 ? origin : $"{sources.TrimEnd()} {origin}";
        return contentSecurityPolicy.Remove(match.Groups["sources"].Index, match.Groups["sources"].Length)
            .Insert(match.Groups["sources"].Index, suffix);
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in connectionString.Split(';'))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = segment.Substring(0, separatorIndex).Trim();
            var value = segment.Substring(separatorIndex + 1).Trim().Trim('"');
            if (key.Length > 0)
            {
                values[key] = value;
            }
        }
        return values;
    }

    private static bool TryGetAccountName(string accountName, out string validatedAccountName)
    {
        validatedAccountName = accountName?.Trim();
        return !string.IsNullOrEmpty(validatedAccountName) &&
            validatedAccountName.All(character =>
                character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9');
    }

    private static bool TryGetEndpointSuffix(string endpointSuffix, out string validatedEndpointSuffix)
    {
        validatedEndpointSuffix = endpointSuffix?.Trim().Trim('/');
        if (string.IsNullOrEmpty(validatedEndpointSuffix))
        {
            return false;
        }
        if (validatedEndpointSuffix.IndexOfAny(['/', '\\', '?', '#', '@']) >= 0 ||
            validatedEndpointSuffix.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return Uri.TryCreate($"https://account.blob.{validatedEndpointSuffix}", UriKind.Absolute,
            out var uri) && uri.Scheme is "https" or "http";
    }

    private static bool TryGetOrigin(string endpoint, out string origin)
    {
        origin = null;
        if (!Uri.TryCreate(endpoint?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        origin = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }
}
