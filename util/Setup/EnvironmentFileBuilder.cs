using System;
using System.Collections.Generic;
using System.IO;

namespace Bit.Setup
{
    public class EnvironmentFileBuilder
    {
        private IDictionary<string, string> _globalValues;
        private IDictionary<string, string> _mssqlValues;

        public string Url { get; set; } = "https://localhost";
        public string Domain { get; set; } = "localhost";
        public string IdentityCertPassword { get; set; } = "REPLACE";
        public Guid? InstallationId { get; set; }
        public string InstallationKey { get; set; }
        public bool Push { get; set; }
        public string DatabasePassword { get; set; } = "REPLACE";
        public string OutputDirectory { get; set; } = ".";

        public void BuildForInstaller()
        {
            Directory.CreateDirectory("/bitwarden/env/");
            Init(true);
            Build();
        }

        public void BuildForUpdater()
        {
            Init(false);
            LoadExistingValues(_globalValues, "/bitwarden/env/global.override.env");
            LoadExistingValues(_mssqlValues, "/bitwarden/env/mssql.override.env");
            Build();
        }

        private void Init(bool forInstall)
        {
            var dbConnectionString = Helpers.MakeSqlConnectionString("mssql", "vault", "sa", DatabasePassword);
            _globalValues = new Dictionary<string, string>
            {
                ["globalSettings__baseServiceUri__vault"] = Url,
                ["globalSettings__baseServiceUri__api"] = $"{Url}/api",
                ["globalSettings__baseServiceUri__identity"] = $"{Url}/identity",
                ["globalSettings__sqlServer__connectionString"] = $"\"{ dbConnectionString }\"",
                ["globalSettings__identityServer__certificatePassword"] = IdentityCertPassword,
                ["globalSettings__attachment__baseDirectory"] = $"{OutputDirectory}/core/attachments",
                ["globalSettings__attachment__baseUrl"] = $"{Url}/attachments",
                ["globalSettings__dataProtection__directory"] = $"{OutputDirectory}/core/aspnet-dataprotection",
                ["globalSettings__logDirectory"] = $"{OutputDirectory}/core/logs",
                ["globalSettings__licenseDirectory"] = $"{OutputDirectory}/core/licenses",
                ["globalSettings__duo__aKey"] = $"{Helpers.SecureRandomString(64, alpha: true, numeric: true)}",
                ["globalSettings__installation__id"] = InstallationId?.ToString(),
                ["globalSettings__installation__key"] = InstallationKey,
                ["globalSettings__yubico__clientId"] = "REPLACE",
                ["globalSettings__yubico__key"] = "REPLACE",
                ["globalSettings__mail__replyToEmail"] = $"no-reply@{Domain}",
                ["globalSettings__mail__smtp__host"] = "REPLACE",
                ["globalSettings__mail__smtp__username"] = "REPLACE",
                ["globalSettings__mail__smtp__password"] = "REPLACE",
                ["globalSettings__mail__smtp__ssl"] = "true",
                ["globalSettings__mail__smtp__port"] = "587",
                ["globalSettings__mail__smtp__useDefaultCredentials"] = "false",
                ["globalSettings__disableUserRegistration"] = "false",
            };

            if(forInstall && !Push)
            {
                _globalValues.Add("globalSettings__pushRelayBaseUri", "REPLACE");
            }

            _mssqlValues = new Dictionary<string, string>
            {
                ["ACCEPT_EULA"] = "Y",
                ["MSSQL_PID"] = "Express",
                ["SA_PASSWORD"] = DatabasePassword,
            };
        }

        private void LoadExistingValues(IDictionary<string, string> _values, string file)
        {
            if(!File.Exists(file))
            {
                return;
            }

            var fileLines = File.ReadAllLines(file);
            foreach(var line in fileLines)
            {
                if(!line.Contains("="))
                {
                    continue;
                }

                var value = string.Empty;
                var lineParts = line.Split("=", 2);
                if(lineParts.Length < 1)
                {
                    continue;
                }

                if(lineParts.Length > 1)
                {
                    value = lineParts[1];
                }

                if(_values.ContainsKey(lineParts[0]))
                {
                    _values[lineParts[0]] = value;
                }
                else
                {
                    _values.Add(lineParts[0], value);
                }
            }
        }

        private void Build()
        {
            Console.WriteLine("Building docker environment files.");
            Directory.CreateDirectory("/bitwarden/docker/");
            using(var sw = File.CreateText("/bitwarden/docker/global.env"))
            {
                sw.Write($@"ASPNETCORE_ENVIRONMENT=Production
globalSettings__selfHosted=true
globalSettings__baseServiceUri__vault=http://localhost
globalSettings__baseServiceUri__api=http://localhost/api
globalSettings__baseServiceUri__identity=http://localhost/identity
globalSettings__baseServiceUri__internalIdentity=http://identity
globalSettings__pushRelayBaseUri=https://push.bitwarden.com
globalSettings__installation__identityUri=https://identity.bitwarden.com
");
            }

            using(var sw = File.CreateText("/bitwarden/docker/mssql.env"))
            {
                sw.Write($@"ACCEPT_EULA=Y
MSSQL_PID=Express
SA_PASSWORD=SECRET
");
            }

            Console.WriteLine("Building docker environment override files.");
            Directory.CreateDirectory(" /bitwarden/env/");
            using(var sw = File.CreateText("/bitwarden/env/global.override.env"))
            {
                foreach(var item in _globalValues)
                {
                    sw.WriteLine($"{item.Key}={item.Value}");
                }
            }

            using(var sw = File.CreateText("/bitwarden/env/mssql.override.env"))
            {
                foreach(var item in _mssqlValues)
                {
                    sw.WriteLine($"{item.Key}={item.Value}");
                }
            }
        }
    }
}
