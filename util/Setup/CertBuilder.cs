namespace Bit.Setup;

public class CertBuilder
{
    private readonly Context _context;

    public CertBuilder(Context context)
    {
        _context = context;
    }

    public void BuildForInstall()
    {
        if (_context.Stub)
        {
            _context.Config.Ssl = true;
            _context.Install.Trusted = true;
            _context.Install.SelfSignedCert = false;
            _context.Install.DiffieHellman = false;
            _context.Install.IdentityCertPassword = "IDENTITY_CERT_PASSWORD";
            return;
        }

        _context.Config.Ssl = _context.Config.SslManagedLetsEncrypt;

        if (!_context.Config.Ssl)
        {
            var skipSSL = _context.Parameters.ContainsKey("skip-ssl") && (_context.Parameters["skip-ssl"] == "true" || _context.Parameters["skip-ssl"] == "1");

            if (!skipSSL)
            {
                _context.Config.Ssl = Helpers.ReadQuestion("Do you have a SSL certificate to use?");
                if (_context.Config.Ssl)
                {
                    Directory.CreateDirectory($"/bitwarden/ssl/{_context.Install.Domain}/");
                    var message = "Make sure 'certificate.crt' and 'private.key' are provided in the \n" +
                                  "appropriate directory before running 'start' (see docs for info).";
                    Helpers.ShowBanner(_context, "NOTE", message);
                }
                else if (Helpers.ReadQuestion("Do you want to generate a self-signed SSL certificate?"))
                {
                    Directory.CreateDirectory($"/bitwarden/ssl/self/{_context.Install.Domain}/");
                    Helpers.WriteLine(_context, "Generating self signed SSL certificate.");
                    _context.Config.Ssl = true;
                    _context.Install.Trusted = false;
                    _context.Install.SelfSignedCert = true;
                    Helpers.Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -days 36500 " +
                                 $"-keyout /bitwarden/ssl/self/{_context.Install.Domain}/private.key " +
                                 $"-out /bitwarden/ssl/self/{_context.Install.Domain}/certificate.crt " +
                                 $"-reqexts SAN -extensions SAN " +
                                 $"-config <(cat /usr/lib/ssl/openssl.cnf <(printf '[SAN]\nsubjectAltName=DNS:{_context.Install.Domain}\nbasicConstraints=CA:true')) " +
                                 $"-subj \"/C=US/ST=California/L=Santa Barbara/O=Bitwarden Inc./OU=Bitwarden/CN={_context.Install.Domain}\"");
                }
            }
        }

        if (_context.Config.SslManagedLetsEncrypt)
        {
            _context.Install.Trusted = true;
            _context.Install.DiffieHellman = true;
            Directory.CreateDirectory($"/bitwarden/letsencrypt/live/{_context.Install.Domain}/");
            Helpers.Exec($"openssl dhparam -out " +
                $"/bitwarden/letsencrypt/live/{_context.Install.Domain}/dhparam.pem 2048");
        }
        else if (_context.Config.Ssl && !_context.Install.SelfSignedCert)
        {
            _context.Install.Trusted = Helpers.ReadQuestion("Is this a trusted SSL certificate " +
                "(requires ca.crt, see docs)?");
        }

        Helpers.WriteLine(_context, "Generating key for IdentityServer.");
        _context.Install.IdentityCertPassword = Helpers.SecureRandomString(32, alpha: true, numeric: true);
        Directory.CreateDirectory("/bitwarden/identity/");
        Helpers.Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity.key " +
            "-out identity.crt -subj \"/CN=Bitwarden IdentityServer\" -days 36500");
        Helpers.Exec("openssl pkcs12 -export -out /bitwarden/identity/identity.pfx -inkey identity.key " +
            $"-in identity.crt -passout pass:{_context.Install.IdentityCertPassword}");

        Helpers.WriteLine(_context);

        if (!_context.Config.Ssl)
        {
            var message = "You are not using a SSL certificate. Bitwarden requires HTTPS to operate. \n" +
                          "You must front your installation with a HTTPS proxy or the web vault (and \n" +
                          "other Bitwarden apps) will not work properly.";
            Helpers.ShowBanner(_context, "WARNING", message, ConsoleColor.Yellow);
        }
        else if (_context.Config.Ssl && !_context.Install.Trusted)
        {
            var message = "You are using an untrusted SSL certificate. This certificate will not be \n" +
                          "trusted by Bitwarden client applications. You must add this certificate to \n" +
                          "the trusted store on each device or else you will receive errors when trying \n" +
                          "to connect to your installation.";
            Helpers.ShowBanner(_context, "WARNING", message, ConsoleColor.Yellow);
        }
    }

    public void BuildForUpdater()
    {
        if (_context.Config.EnableKeyConnector && !File.Exists("/bitwarden/key-connector/bwkc.pfx"))
        {
            Directory.CreateDirectory("/bitwarden/key-connector/");
            var keyConnectorCertPassword = Helpers.GetValueFromEnvFile("key-connector",
                "keyConnectorSettings__certificate__filesystemPassword");
            Helpers.Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout bwkc.key " +
                         "-out bwkc.crt -subj \"/CN=Bitwarden Key Connector\" -days 36500");
            Helpers.Exec("openssl pkcs12 -export -out /bitwarden/key-connector/bwkc.pfx -inkey bwkc.key " +
                         $"-in bwkc.crt -passout pass:{keyConnectorCertPassword}");
        }
    }
}
