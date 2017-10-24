using System;
using System.IO;

namespace Bit.Setup
{
    public class EnvironmentFileBuilder
    {
        public string Url { get; set; }
        public string Domain { get; set; }
        public string IdentityCertPassword { get; set; }
        public Guid? InstallationId { get; set; }
        public string InstallationKey { get; set; }
        public bool Push { get; set; }
        public string DatabasePassword { get; set; }
        public string OutputDirectory { get; set; }

        public void Build()
        {
            Console.WriteLine("Building docker environment override files.");
            Directory.CreateDirectory("/bitwarden/env/");
            var dbConnectionString = Helpers.MakeSqlConnectionString("mssql", "vault", "sa", DatabasePassword);

            using(var sw = File.CreateText("/bitwarden/env/global.override.env"))
            {
                sw.Write($@"globalSettings__baseServiceUri__vault={Url}
globalSettings__baseServiceUri__api={Url}/api
globalSettings__baseServiceUri__identity={Url}/identity
globalSettings__sqlServer__connectionString=""{dbConnectionString}""
globalSettings__identityServer__certificatePassword={IdentityCertPassword}
globalSettings__attachment__baseDirectory={OutputDirectory}/core/attachments
globalSettings__attachment__baseUrl={Url}/attachments
globalSettings__dataProtection__directory={OutputDirectory}/core/aspnet-dataprotection
globalSettings__logDirectory={OutputDirectory}/core/logs
globalSettings__licenseDirectory={OutputDirectory}/core/licenses
globalSettings__duo__aKey={Helpers.SecureRandomString(64, alpha: true, numeric: true)}
globalSettings__installation__id={InstallationId}
globalSettings__installation__key={InstallationKey}
globalSettings__yubico__clientId=REPLACE
globalSettings__yubico__key=REPLACE
globalSettings__mail__replyToEmail=no-reply@{Domain}
globalSettings__mail__smtp__host=REPLACE
globalSettings__mail__smtp__username=REPLACE
globalSettings__mail__smtp__password=REPLACE
globalSettings__mail__smtp__ssl=true
globalSettings__mail__smtp__port=587
globalSettings__mail__smtp__useDefaultCredentials=false
globalSettings__disableUserRegistration=false");

                if(!Push)
                {
                    sw.Write(@"
globalSettings__pushRelayBaseUri=REPLACE");
                }
            }

            using(var sw = File.CreateText("/bitwarden/env/mssql.override.env"))
            {
                sw.Write($@"ACCEPT_EULA=Y
MSSQL_PID=Express
SA_PASSWORD={DatabasePassword}");
            }
        }
    }
}
