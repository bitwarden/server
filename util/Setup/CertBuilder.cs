using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Bit.Setup
{
    public class CertBuilder
    {
        public CertBuilder(string domain, string identityCertPassword, bool letsEncrypt, bool ssl)
        {
            Domain = domain;
            IdentityCertPassword = identityCertPassword;
            LetsEncrypt = letsEncrypt;
            Ssl = ssl;
        }

        public string Domain { get; private set; }
        public bool LetsEncrypt { get; private set; }
        public bool Ssl { get; private set; }
        public string IdentityCertPassword { get; private set; }

        public bool BuildForInstall()
        {
            var selfSignedSsl = false;
            if(!Ssl)
            {
                Directory.CreateDirectory($"/bitwarden/ssl/self/{Domain}/");
                Console.WriteLine("Generating self signed SSL certificate.");
                Ssl = selfSignedSsl = true;
                Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -days 365 " +
                    $"-keyout /bitwarden/ssl/self/{Domain}/private.key " +
                    $"-out /bitwarden/ssl/self/{Domain}/certificate.crt " +
                    $"-subj \"/C=US/ST=New York/L=New York/O=8bit Solutions LLC/OU=bitwarden/CN={Domain}\"");
            }

            if(LetsEncrypt)
            {
                Directory.CreateDirectory($"/bitwarden/letsencrypt/live/{Domain}/");
                Exec($"openssl dhparam -out /bitwarden/letsencrypt/live/{Domain}/dhparam.pem 2048");
            }

            Console.WriteLine("Generating key for IdentityServer.");
            Directory.CreateDirectory("/bitwarden/identity/");
            Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity.key " +
                "-out identity.crt -subj \"/CN=bitwarden IdentityServer\" -days 10950");
            Exec("openssl pkcs12 -export -out /bitwarden/identity/identity.pfx -inkey identity.key " +
                $"-in identity.crt -certfile identity.crt -passout pass:{IdentityCertPassword}");

            return selfSignedSsl;
        }

        private string Exec(string cmd)
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
            var result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}
