namespace Bit.Setup;

public class NginxConfigBuilder
{
    private const string ConfFile = "/bitwarden/nginx/default.conf";

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
        Directory.CreateDirectory("/bitwarden/nginx/");
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
            Captcha = context.Config.Captcha;
            Ssl = context.Config.Ssl;
            EnableKeyConnector = context.Config.EnableKeyConnector;
            EnableScim = context.Config.EnableScim;
            Domain = context.Config.Domain;
            Url = context.Config.Url;
            RealIps = context.Config.RealIps;
            ContentSecurityPolicy = string.Format(context.Config.NginxHeaderContentSecurityPolicy, Domain);

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
                SslCiphers = "ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384:" +
                    "ECDHE-ECDSA-CHACHA20-POLY1305:ECDHE-RSA-CHACHA20-POLY1305:ECDHE-ECDSA-AES128-GCM-SHA256:" +
                    "ECDHE-RSA-AES128-GCM-SHA256:ECDHE-ECDSA-AES256-SHA384:ECDHE-RSA-AES256-SHA384:" +
                    "ECDHE-ECDSA-AES128-SHA256:ECDHE-RSA-AES128-SHA256";
            }

            if (!string.IsNullOrWhiteSpace(context.Config.SslVersions))
            {
                SslProtocols = context.Config.SslVersions;
            }
            else
            {
                SslProtocols = "TLSv1.2";
            }
        }

        public bool Captcha { get; set; }
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
        public string ContentSecurityPolicy { get; set; }
        public List<string> RealIps { get; set; }
    }
}
