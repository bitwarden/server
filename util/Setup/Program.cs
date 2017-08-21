using DbUp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Setup
{
    public class Program
    {
        private static string[] _args = null;
        private static IDictionary<string, string> _parameters = null;
        private static string _outputDir = "/etc/bitwarden";
        private static string _domain = null;
        private static string _url = null;
        private static string _identityCertPassword = null;
        private static bool _ssl = false;
        private static bool _selfSignedSsl = false;
        private static bool _letsEncrypt = false;
        private static Guid? _installationId = null;
        private static string _installationKey = null;
        private static bool _push = false;

        public static void Main(string[] args)
        {
            _args = args;
            _parameters = ParseParameters();
            if(_parameters.ContainsKey("install"))
            {
                Install();
            }
            else if(_parameters.ContainsKey("update"))
            {
                Update();
            }
            else
            {
                Console.WriteLine("No top-level command detected. Exiting...");
            }
        }

        private static void Install()
        {
            _outputDir = _parameters.ContainsKey("out") ?
                _parameters["out"].ToLowerInvariant() : _outputDir;
            _domain = _parameters.ContainsKey("domain") ?
                _parameters["domain"].ToLowerInvariant() : "localhost";
            _letsEncrypt = _parameters.ContainsKey("letsencrypt") ?
                _parameters["letsencrypt"].ToLowerInvariant() == "y" : false;

            if(!ValidateInstallation())
            {
                return;
            }

            _ssl = _letsEncrypt;
            if(!_letsEncrypt)
            {
                Console.Write("(!) Do you have a SSL certificate to use? (y/n): ");
                _ssl = Console.ReadLine().ToLowerInvariant() == "y";

                if(_ssl)
                {
                    Console.WriteLine("Make sure 'certificate.crt' and 'private.key' are provided in the " +
                        "appropriate directory (see setup instructions).");
                }
            }

            _identityCertPassword = Helpers.SecureRandomString(32, alpha: true, numeric: true);
            MakeCerts();

            _url = _ssl ? $"https://{_domain}" : $"http://{_domain}";
            BuildNginxConfig();

            Console.Write("(!) Do you want to use push notifications? (y/n): ");
            _push = Console.ReadLine().ToLowerInvariant() == "y";

            BuildEnvironmentFiles();
            BuildAppSettingsFiles();
            BuildAppId();
        }

        private static void Update()
        {
            if(_parameters.ContainsKey("db"))
            {
                MigrateDatabase();
            }
        }

        private static void MigrateDatabase()
        {
            Console.WriteLine("Migrating database.");

            var dbPass = Helpers.GetDatabasePasswordFronEnvFile();
            var masterConnectionString = Helpers.MakeSqlConnectionString("mssql", "master", "sa", dbPass ?? string.Empty);
            var vaultConnectionString = Helpers.MakeSqlConnectionString("mssql", "vault", "sa", dbPass ?? string.Empty);

            using(var connection = new SqlConnection(masterConnectionString))
            {
                var command = new SqlCommand(
                    "IF ((SELECT COUNT(1) FROM sys.databases WHERE [name] = 'vault') = 0) CREATE DATABASE [vault];",
                    connection);
                command.Connection.Open();
                command.ExecuteNonQuery();
            }

            var upgrader = DeployChanges.To
                .SqlDatabase(vaultConnectionString)
                .JournalToSqlTable("dbo", "Migration")
                .WithScriptsAndCodeEmbeddedInAssembly(Assembly.GetExecutingAssembly(),
                    s => s.Contains($".DbScripts.") && !s.Contains(".Archive."))
                .WithTransaction()
                .WithExecutionTimeout(new TimeSpan(0, 5, 0))
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();
            if(result.Successful)
            {
                Console.WriteLine("Migration successful.");
            }
            else
            {
                Console.WriteLine("Migration failed.");
            }
        }

        private static bool ValidateInstallation()
        {
            Console.Write("(!) Enter your installation id (get it at https://bitwarden.com/install): ");
            var installationId = Console.ReadLine();
            Guid installationidGuid;
            if(!Guid.TryParse(installationId.Trim(), out installationidGuid))
            {
                Console.WriteLine("Invalid installation id.");
                return false;
            }
            _installationId = installationidGuid;

            Console.Write("(!) Enter your installation key: ");
            _installationKey = Console.ReadLine();

            // validate all installations for now. remove later.
            return true;

            try
            {
                var response = new HttpClient().GetAsync("https://api.bitwarden.com/installations/" + _installationId)
                    .GetAwaiter().GetResult();

                if(!response.IsSuccessStatusCode)
                {
                    if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Console.WriteLine("Invalid installation id.");
                    }
                    else
                    {
                        Console.WriteLine("Unable to validate installation id.");
                    }

                    return false;
                }

                var resultString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var result = JsonConvert.DeserializeObject<dynamic>(resultString);
                if(!(bool)result.Enabled)
                {
                    Console.WriteLine("Installation id has been disabled.");
                    return false;
                }

                return true;
            }
            catch
            {
                Console.WriteLine("Unable to validate installation id. Problem contacting bitwarden server.");
                return false;
            }
        }

        private static void MakeCerts()
        {
            if(!_ssl)
            {
                Console.Write("(!) Do you want to generate a self-signed SSL certificate? (y/n): ");
                if(Console.ReadLine().ToLowerInvariant() == "y")
                {
                    Directory.CreateDirectory($"/bitwarden/ssl/self/{_domain}/");
                    Console.WriteLine("Generating self signed SSL certificate.");
                    _ssl = _selfSignedSsl = true;
                    Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -days 365 " +
                        $"-keyout /bitwarden/ssl/self/{_domain}/private.key " +
                        $"-out /bitwarden/ssl/self/{_domain}/certificate.crt " +
                        $"-subj \"/C=US/ST=New York/L=New York/O=8bit Solutions LLC/OU=bitwarden/CN={_domain}\"");
                }
            }

            if(_letsEncrypt)
            {
                Directory.CreateDirectory($"/bitwarden/letsencrypt/live/{_domain}/");
                Exec($"openssl dhparam -out /bitwarden/letsencrypt/live/{_domain}/dhparam.pem 2048");
            }

            Console.WriteLine("Generating key for IdentityServer.");
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

            var dh = _letsEncrypt;
            if(_ssl && !_selfSignedSsl && !_letsEncrypt)
            {
                Console.Write("(!) Use Diffie Hellman ephemeral parameters for SSL (requires dhparam.pem)? (y/n): ");
                dh = Console.ReadLine().ToLowerInvariant() == "y";
            }

            var trusted = _letsEncrypt;
            if(_ssl && !_selfSignedSsl && !_letsEncrypt)
            {
                Console.Write("(!) Is this a trusted SSL certificate (requires ca.crt)? (y/n): ");
                trusted = Console.ReadLine().ToLowerInvariant() == "y";
            }

            var sslPath = _letsEncrypt ? $"/etc/letsencrypt/live/{_domain}" :
                _selfSignedSsl ? $"/etc/ssl/self/{_domain}" : $"/etc/ssl/{_domain}";
            var certFile = _letsEncrypt ? "fullchain.pem" : "certificate.crt";
            var keyFile = _letsEncrypt ? "privkey.pem" : "private.key";
            var caFile = _letsEncrypt ? "fullchain.pem" : "ca.crt";

            Console.WriteLine("Building nginx config.");
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

    ssl_certificate {sslPath}/{certFile};
    ssl_certificate_key {sslPath}/{keyFile};
    
    ssl_session_timeout 30m;
    ssl_session_cache shared:SSL:20m;
    ssl_session_tickets off;");

                    if(dh)
                    {
                        sw.WriteLine($@"
    # Diffie-Hellman parameter for DHE ciphersuites, recommended 2048 bits
    ssl_dhparam {sslPath}/dhparam.pem;");
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
    ssl_trusted_certificate {sslPath}/{caFile};

    resolver 8.8.8.8 8.8.4.4 208.67.222.222 208.67.220.220 valid=300s;

    # This will enforce HTTP browsing into HTTPS and avoid ssl stripping attack. 6 months age
    add_header Strict-Transport-Security max-age=15768000;");
                    }
                }

                sw.WriteLine($@"
    # X-Frame-Options is to prevent from clickJacking attack
    add_header X-Frame-Options SAMEORIGIN;

    # disable content-type sniffing on some browsers.
    add_header X-Content-Type-Options nosniff;

    # This header enables the Cross-site scripting (XSS) filter
    add_header X-XSS-Protection ""1; mode=block"";

    # This header controls what referrer information is shared
    add_header Referrer-Policy same-origin;

    # Content-Security-Policy is set via meta tag on the website so it is not included here");

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

    location /attachments/ {{
        proxy_pass http://attachments/;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Url-Scheme $scheme;
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
            Console.WriteLine("Building docker environment override files.");
            Directory.CreateDirectory("/bitwarden/env/");
            var dbPass = Helpers.SecureRandomString(32);
            var dbConnectionString = Helpers.MakeSqlConnectionString("mssql", "vault", "sa", dbPass);

            using(var sw = File.CreateText("/bitwarden/env/global.override.env"))
            {
                sw.Write($@"globalSettings__baseServiceUri__vault={_url}
globalSettings__baseServiceUri__api={_url}/api
globalSettings__baseServiceUri__identity={_url}/identity
globalSettings__sqlServer__connectionString=""{dbConnectionString}""
globalSettings__identityServer__certificatePassword={_identityCertPassword}
globalSettings__attachment__baseDirectory={_outputDir}/core/attachments
globalSettings__attachment__baseUrl={_url}/attachments
globalSettings__dataProtection__directory={_outputDir}/core/aspnet-dataprotection
globalSettings__logDirectory={_outputDir}/core/logs
globalSettings__licenseDirectory={_outputDir}/core/licenses
globalSettings__duo__aKey={Helpers.SecureRandomString(64, alpha: true, numeric: true)}
globalSettings__installation__id={_installationId}
globalSettings__installation__key={_installationKey}
globalSettings__yubico__clientId=REPLACE
globalSettings__yubico__key=REPLACE");

                if(!_push)
                {
                    sw.Write(@"
globalSettings__pushRelayBaseUri=REPLACE");
                }
            }

            using(var sw = File.CreateText("/bitwarden/env/mssql.override.env"))
            {
                sw.Write($@"ACCEPT_EULA=Y
MSSQL_PID=Express
SA_PASSWORD={dbPass}");
            }
        }

        private static void BuildAppSettingsFiles()
        {
            Console.WriteLine("Building app settings.");
            Directory.CreateDirectory("/bitwarden/web/");
            using(var sw = File.CreateText("/bitwarden/web/settings.js"))
            {
                sw.Write($@"var bitwardenAppSettings = {{
    apiUri: ""{_url}/api"",
    identityUri: ""{_url}/identity"",
    stripeKey: null,
    braintreeKey: null,
    whitelistDomains: [""{_domain}""],
    selfHosted: true
}};");
            }
        }

        private static void BuildAppId()
        {
            Console.WriteLine("Building FIDO U2F app id.");
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
