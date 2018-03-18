using System;
using System.IO;

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
                Console.Write("(!) Do you want to generate a self-signed SSL certificate? (y/n): ");
                if(Console.ReadLine().ToLowerInvariant() == "y")
                {
                    Directory.CreateDirectory($"/bitwarden/ssl/self/{Domain}/");
                    Console.WriteLine("Generating self signed SSL certificate.");
                    Ssl = selfSignedSsl = true;
                    Helpers.Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -days 365 " +
                        $"-keyout /bitwarden/ssl/self/{Domain}/private.key " +
                        $"-out /bitwarden/ssl/self/{Domain}/certificate.crt " +
                        $"-subj \"/C=US/ST=New York/L=New York/O=8bit Solutions LLC/OU=Bitwarden/CN={Domain}\"");
                }
                else
                {
                    Console.WriteLine("!!!!!! WARNING !!!!!!");
                    Console.WriteLine("You are not using an SSL certificate. Bitwarden requires HTTPS to operate. " +
                        "You must front your installation with a HTTPS proxy. The web vault (and other Bitwarden " +
                        "apps) will not work properly without HTTPS.");
                    Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!");
                }
            }

            if(LetsEncrypt)
            {
                Directory.CreateDirectory($"/bitwarden/letsencrypt/live/{Domain}/");
                Helpers.Exec($"openssl dhparam -out /bitwarden/letsencrypt/live/{Domain}/dhparam.pem 2048");
            }

            Console.WriteLine("Generating key for IdentityServer.");
            Directory.CreateDirectory("/bitwarden/identity/");
            Helpers.Exec("openssl req -x509 -newkey rsa:4096 -sha256 -nodes -keyout identity.key " +
                "-out identity.crt -subj \"/CN=Bitwarden IdentityServer\" -days 10950");
            Helpers.Exec("openssl pkcs12 -export -out /bitwarden/identity/identity.pfx -inkey identity.key " +
                $"-in identity.crt -certfile identity.crt -passout pass:{IdentityCertPassword}");

            return selfSignedSsl;
        }
    }
}
