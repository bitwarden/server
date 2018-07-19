using System;
using System.IO;

namespace Bit.Setup
{
    public class NginxConfigBuilder
    {
        private const string ConfFile = "/bitwarden/nginx/default.conf";
        private const string SslCiphers =
            "ECDHE-RSA-AES256-GCM-SHA384:ECDHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384:" +
            "DHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-SHA384:ECDHE-RSA-AES128-SHA256:ECDHE-RSA-AES256-SHA:" +
            "ECDHE-RSA-AES128-SHA:DHE-RSA-AES256-SHA256:DHE-RSA-AES128-SHA256:DHE-RSA-AES256-SHA:DHE-RSA-AES128-SHA:" +
            "ECDHE-RSA-DES-CBC3-SHA:EDH-RSA-DES-CBC3-SHA:AES256-GCM-SHA384:AES128-GCM-SHA256:AES256-SHA256:" +
            "AES128-SHA256:AES256-SHA:AES128-SHA:DES-CBC3-SHA:HIGH:!aNULL:!eNULL:!EXPORT:!DES:!MD5:!PSK:!RC4:@STRENGTH";
        private const string ContentSecurityPolicy =
            "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://haveibeenpwned.com; " +
            "child-src 'self' https://*.duosecurity.com; frame-src 'self' https://*.duosecurity.com; " +
            "connect-src 'self' https://haveibeenpwned.com https://api.pwnedpasswords.com;";

        public NginxConfigBuilder(string domain, string url, bool ssl, bool selfSignedSsl, bool letsEncrypt,
            bool trusted, bool diffieHellman)
        {
            Domain = domain;
            Url = url;
            Ssl = ssl;
            SelfSignedSsl = selfSignedSsl;
            LetsEncrypt = letsEncrypt;
            Trusted = trusted;
            DiffieHellman = diffieHellman;
        }

        public NginxConfigBuilder(string domain, string url)
        {
            Domain = domain;
            Url = url;
        }

        public bool Ssl { get; private set; }
        public bool SelfSignedSsl { get; private set; }
        public bool LetsEncrypt { get; private set; }
        public string Domain { get; private set; }
        public string Url { get; private set; }
        public bool DiffieHellman { get; private set; }
        public bool Trusted { get; private set; }

        public void BuildForInstaller()
        {
            Build();
        }

        public void BuildForUpdater()
        {
            if(File.Exists(ConfFile))
            {
                var confContent = File.ReadAllText(ConfFile);
                Ssl = confContent.Contains("ssl http2;");
                SelfSignedSsl = confContent.Contains("/etc/ssl/self/");
                LetsEncrypt = !SelfSignedSsl && confContent.Contains("/etc/letsencrypt/live/");
                DiffieHellman = confContent.Contains("/dhparam.pem;");
                Trusted = confContent.Contains("ssl_trusted_certificate ");
            }

            Build();
        }

        private void Build()
        {
            Directory.CreateDirectory("/bitwarden/nginx/");

            var sslPath = LetsEncrypt ? $"/etc/letsencrypt/live/{Domain}" :
                SelfSignedSsl ? $"/etc/ssl/self/{Domain}" : $"/etc/ssl/{Domain}";
            var certFile = LetsEncrypt ? "fullchain.pem" : "certificate.crt";
            var keyFile = LetsEncrypt ? "privkey.pem" : "private.key";
            var caFile = LetsEncrypt ? "fullchain.pem" : "ca.crt";

            Console.WriteLine("Building nginx config.");
            using(var sw = File.CreateText(ConfFile))
            {
                sw.WriteLine($@"# Config Parameters
# Parameter:Ssl={Ssl}
# Parameter:SelfSignedSsl={SelfSignedSsl}
# Parameter:LetsEncrypt={LetsEncrypt}
# Parameter:Domain={Domain}
# Parameter:Url={Url}
# Parameter:DiffieHellman={DiffieHellman}
# Parameter:Trusted={Trusted}

server {{
  listen 8080 default_server;
  listen [::]:8080 default_server;
  server_name {Domain};");

                if(Ssl)
                {
                    sw.WriteLine($@"  return 301 {Url}$request_uri;
}}

server {{
  listen 8443 ssl http2;
  listen [::]:8443 ssl http2;
  server_name {Domain};

  ssl_certificate {sslPath}/{certFile};
  ssl_certificate_key {sslPath}/{keyFile};

  ssl_session_timeout 30m;
  ssl_session_cache shared:SSL:20m;
  ssl_session_tickets off;");

                    if(DiffieHellman)
                    {
                        sw.WriteLine($@"
  # Diffie-Hellman parameter for DHE ciphersuites, recommended 2048 bits
  ssl_dhparam {sslPath}/dhparam.pem;");
                    }

                    sw.WriteLine($@"
  # SSL protocols TLS v1~TLSv1.2 are allowed. Disabed SSLv3
  ssl_protocols TLSv1 TLSv1.1 TLSv1.2;
  # Disabled insecure ciphers suite. For example, MD5, DES, RC4, PSK
  ssl_ciphers ""{SslCiphers}"";
  # Enables server-side protection from BEAST attacks
  ssl_prefer_server_ciphers on;");

                    if(Trusted)
                    {
                        sw.WriteLine($@"
  # OCSP Stapling ---
  # Fetch OCSP records from URL in ssl_certificate and cache them
  ssl_stapling on;
  ssl_stapling_verify on;

  # Verify chain of trust of OCSP response using Root CA and Intermediate certs
  ssl_trusted_certificate {sslPath}/{caFile};

  resolver 8.8.8.8 8.8.4.4 208.67.222.222 208.67.220.220 valid=300s;

  # This will enforce HTTP browsing into HTTPS and avoid ssl stripping attack. 6 months age
  add_header Strict-Transport-Security max-age=15768000;");
                    }
                }

                sw.WriteLine($@"
  # X-Frame-Options is to prevent from click-jacking attack
  #add_header X-Frame-Options SAMEORIGIN;

  # Disable content-type sniffing on some browsers.
  add_header X-Content-Type-Options nosniff;

  # This header enables the Cross-site scripting (XSS) filter
  add_header X-XSS-Protection ""1; mode=block"";

  # This header controls what referrer information is shared
  add_header Referrer-Policy same-origin;

  # Content-Security-Policy to prevent malicious XSS code
  add_header Content-Security-Policy {ContentSecurityPolicy}");

                sw.WriteLine($@"
  location / {{
    proxy_pass http://web:5000/;
  }}

  location = /app-id.json {{
    proxy_pass http://web:5000/app-id.json;
    proxy_hide_header Content-Type;
    add_header Content-Type $fido_content_type;
  }}

  location /attachments/ {{
    proxy_pass http://attachments:5000/;
  }}

  location /api/ {{
    proxy_pass http://api:5000/;
  }}

  location /identity/ {{
    proxy_pass http://identity:5000/;
  }}

  location /icons/ {{
    proxy_pass http://icons:5000/;
  }}

  location /admin {{
    proxy_pass http://admin:5000;
  }}
}}");
            }
        }
    }
}
