using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Setup
{
    public class Program
    {
        private static string[] _args = null;
        private static IDictionary<string, string> _parameters = null;
        private static string _domain = null;
        private static string _url = null;
        private static string _identityCertPassword = null;
        private static bool _ssl = false;
        private static bool _letsEncrypt = false;

        public static void Main(string[] args)
        {
            _args = args;
            _parameters = ParseParameters();

            _domain = _parameters.ContainsKey("domain") ?
                _parameters["domain"].ToLowerInvariant() : "localhost";
            _letsEncrypt = _parameters.ContainsKey("letsencrypt") ?
                _parameters["letsencrypt"].ToLowerInvariant() == "y" : false;
            _ssl = _letsEncrypt || (_parameters.ContainsKey("ssl") ?
                _parameters["ssl"].ToLowerInvariant() == "y" : false);
            _url = _ssl ? $"https://{_domain}" : $"http://{_domain}";
            _identityCertPassword = Helpers.SecureRandomString(32, alpha: true, numeric: true);

            MakeCerts();
            BuildNginxConfig();
            BuildEnvironmentFiles();
            BuildAppSettingsFiles();
        }

        private static void MakeCerts()
        {
            Directory.CreateDirectory("/bitwarden/identity/");
            Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity.key " +
                "-out identity.crt -subj \"/CN=bitwarden IdentityServer\" -days 10950");
            Exec("openssl pkcs12 -export -out /bitwarden/identity/identity.pfx -inkey identity.key " +
                $"-in identity.crt -certfile identity.crt -passout pass:{_identityCertPassword}");
        }

        private static void BuildNginxConfig()
        {
            Directory.CreateDirectory("/bitwarden/nginx/");
            var sslCiphers = "ECDHE-RSA-AES256-GCM-SHA384:ECDHE-RSA-AES128-GCM-SHA256:DHE-RSA-AES256-GCM-SHA384:" +
                "DHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-SHA384:ECDHE-RSA-AES128-SHA256:ECDHE-RSA-AES256-SHA:" +
                "ECDHE-RSA-AES128-SHA:DHE-RSA-AES256-SHA256:DHE-RSA-AES128-SHA256:DHE-RSA-AES256-SHA:DHE-RSA-AES128-SHA:" +
                "ECDHE-RSA-DES-CBC3-SHA:EDH-RSA-DES-CBC3-SHA:AES256-GCM-SHA384:AES128-GCM-SHA256:AES256-SHA256:AES128-SHA256:" +
                "AES256-SHA:AES128-SHA:DES-CBC3-SHA:HIGH:!aNULL:!eNULL:!EXPORT:!DES:!MD5:!PSK:!RC4:@STRENGTH";

            var dh = _letsEncrypt ||
                (_parameters.ContainsKey("ssl_dh") ? _parameters["ssl_dh"].ToLowerInvariant() == "y" : false);
            var trusted = _letsEncrypt ||
                (_parameters.ContainsKey("ssl_trusted") ? _parameters["ssl_trusted"].ToLowerInvariant() == "y" : false);
            var certPath = _letsEncrypt ? $"/etc/letsencrypt/live/{_domain}" : $"/etc/certificates/{_domain}";

            using(var sw = File.CreateText("/bitwarden/nginx/default.conf"))
            {
                sw.WriteLine($@"server {{
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name {_domain};");

                if(_ssl)
                {
                    sw.WriteLine($@"    return 301 https://$server_name$request_uri;
}}

server {{
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name {_domain};

    ssl_certificate {certPath}/fullchain.pem;
    ssl_certificate_key {certPath}/privkey.pem;
    
    ssl_session_timeout 30m;
    ssl_session_cache shared:SSL:20m;
    ssl_session_tickets off;");

                    if(dh)
                    {
                        sw.WriteLine($@"
    # Diffie-Hellman parameter for DHE ciphersuites, recommended 2048 bits
    ssl_dhparam {certPath}/dhparam.pem;");
                    }

                    sw.WriteLine($@"
    # SSL protocols TLS v1~TLSv1.2 are allowed. Disabed SSLv3
    ssl_protocols TLSv1 TLSv1.1 TLSv1.2;
    # Disabled insecure ciphers suite. For example, MD5, DES, RC4, PSK
    ssl_ciphers ""{sslCiphers}"";
    # enables server-side protection from BEAST attacks
    ssl_prefer_server_ciphers on;");

                    if(trusted)
                    {
                        sw.WriteLine($@"
    # OCSP Stapling ---
    # fetch OCSP records from URL in ssl_certificate and cache them
    ssl_stapling on;
    ssl_stapling_verify on;

    ## verify chain of trust of OCSP response using Root CA and Intermediate certs
    ssl_trusted_certificate {certPath}/fullchain.pem;

    resolver 8.8.8.8 8.8.4.4 208.67.222.222 208.67.220.220 valid=300s;");
                    }

                    sw.WriteLine($@"
    # Headers

    # X-Frame-Options is to prevent from clickJacking attack
    add_header X-Frame-Options SAMEORIGIN;

    # disable content-type sniffing on some browsers.
    add_header X-Content-Type-Options nosniff;

    # This header enables the Cross-site scripting (XSS) filter
    add_header X-XSS-Protection ""1; mode=block"";

    # This header controls what referrer information is shared
    add_header Referrer-Policy same-origin;

    # This will enforce HTTP browsing into HTTPS and avoid ssl stripping attack. 6 months age
    add_header Strict-Transport-Security max-age=15768000;

    # Content-Security-Policy is set via meta tag on the website so it is not included here");
                }

                sw.WriteLine($@"
    location / {{
        proxy_pass http://web/;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Url-Scheme $scheme;
        proxy_redirect off;
    }}

    location = /app-id.json {{
        proxy_pass http://web/app-id.json;
        proxy_hide_header Content-Type;
        add_header Content-Type $fido_content_type;
        proxy_redirect off;
    }}

    location /api/ {{
        proxy_pass http://api/;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Url-Scheme $scheme;
        proxy_redirect off;
    }}

    location /identity/ {{
        proxy_pass http://identity/;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Url-Scheme $scheme;
        proxy_redirect off;
    }}
}}");
            }
        }

        private static void BuildEnvironmentFiles()
        {
            Directory.CreateDirectory("/bitwarden/docker/");
            var dbPass = _parameters.ContainsKey("db_pass") ? _parameters["db_pass"].ToLowerInvariant() : "REPLACE";
            var dbConnectionString = "Server=tcp:mssql,1433;Initial Catalog=vault;Persist Security Info=False;User ID=sa;" +
                $"Password={dbPass};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;" +
                "Connection Timeout=30;";

            using(var sw = File.CreateText("/bitwarden/docker/global.override.env"))
            {
                sw.Write($@"globalSettings:baseServiceUri:vault={_url}
globalSettings:baseServiceUri:api={_url}/api
globalSettings:baseServiceUri:identity={_url}/identity
globalSettings:sqlServer:connectionString={dbConnectionString}
globalSettings:identityServer:certificatePassword={_identityCertPassword}
globalSettings:duo:aKey={Helpers.SecureRandomString(32, alpha: true, numeric: true)}
globalSettings:yubico:clientId=REPLACE
globalSettings:yubico:REPLACE");
            }

            using(var sw = File.CreateText("/bitwarden/docker/mssql.override.env"))
            {
                sw.Write($@"ACCEPT_EULA=Y
MSSQL_PID=Express
SA_PASSWORD={dbPass}");
            }
        }

        private static void BuildAppSettingsFiles()
        {
            Directory.CreateDirectory("/bitwarden/web/");
            using(var sw = File.CreateText("/bitwarden/web/settings.js"))
            {
                sw.Write($@"var bitwardenAppSettings = {{
    apiUri: ""{_url}/api"",
    identityUri: ""{_url}/identity"",
    whitelistDomains: [""{_domain}""]
}};");
            }
        }

        private static void BuildAppId()
        {
            Directory.CreateDirectory("/bitwarden/web/");
            using(var sw = File.CreateText("/bitwarden/web/app-id.json"))
            {
                sw.Write($@"{{
  ""trustedFacets"": [
    {{
      ""version"": {{
        ""major"": 1,
        ""minor"": 0
      }},
      ""ids"": [
        ""{_url}"",
        ""ios:bundle-id:com.8bit.bitwarden"",
        ""android:apk-key-hash:dUGFzUzf3lmHSLBDBIv+WaFyZMI""
      ]
    }}
  ]
}}");
            }
        }

        private static IDictionary<string, string> ParseParameters()
        {
            var dict = new Dictionary<string, string>();
            for(var i = 0; i < _args.Length; i = i + 2)
            {
                if(!_args[i].StartsWith("-"))
                {
                    continue;
                }

                dict.Add(_args[i].Substring(1), _args[i + 1]);
            }

            return dict;
        }

        private static string Exec(string cmd)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var escapedArgs = cmd.Replace("\"", "\\\"");
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            }
            else
            {
                process.StartInfo.FileName = "powershell";
                process.StartInfo.Arguments = cmd;
            }

            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}
