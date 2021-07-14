using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

namespace Bit.Setup
{
    public class EnvironmentFileBuilder
    {
        private readonly Context _context;

        private IDictionary<string, string> _globalValues;
        private IDictionary<string, string> _databaseValues;
        private IDictionary<string, string> _globalOverrideValues;
        private IDictionary<string, string> _databaseOverrideValues;

        public EnvironmentFileBuilder(Context context)
        {
            _context = context;
            _globalValues = new Dictionary<string, string>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
                ["globalSettings__selfHosted"] = "true",
                ["globalSettings__baseServiceUri__vault"] = "http://localhost",
                ["globalSettings__pushRelayBaseUri"] = "https://push.bitwarden.com",
            };
            _databaseValues = new Dictionary<string, string>();
            if (_context.Config.DatabaseDockerType == "mssql")
            {
                _databaseValues.Add("ACCEPT_EULA", "Y");
                _databaseValues.Add("MSSQL_PID", "Express");
                _databaseValues.Add("SA_PASSWORD", "SECRET");
            }
            else if (_context.Config.DatabaseDockerType == "mysql")
            {
                // TODO: Add values specific to running a MySQL container.
            }
            else if (_context.Config.DatabaseDockerType == "postgresql")
            {
                // TODO: ADD values specific to running a PostgreSQL container.
            }
            else
            {
                // TODO: What should we do if the value doesn't match? Assume MSSQL?
            }
        }

        public void BuildForInstaller()
        {
            Directory.CreateDirectory("/bitwarden/env/");
            Init();
            Build();
        }

        public void BuildForUpdater()
        {
            Init();
            LoadExistingValues(_globalOverrideValues, "/bitwarden/env/global.override.env");
            LoadExistingValues(_databaseOverrideValues, "/bitwarden/env/database.override.env");

            if (_context.Config.PushNotifications &&
                _globalOverrideValues.ContainsKey("globalSettings__pushRelayBaseUri") &&
                _globalOverrideValues["globalSettings__pushRelayBaseUri"] == "REPLACE")
            {
                _globalOverrideValues.Remove("globalSettings__pushRelayBaseUri");
            }

            Build();
        }

        private void Init()
        {
            var dbPassword = _context.Stub ? "RANDOM_DATABASE_PASSWORD" : Helpers.SecureRandomString(32);
            var dbConnectionString = new SqlConnectionStringBuilder
            {
                DataSource = "tcp:mssql,1433",
                InitialCatalog = _context.Install?.Database ?? "vault",
                UserID = "sa",
                Password = dbPassword,
                MultipleActiveResultSets = false,
                Encrypt = true,
                ConnectTimeout = 30,
                TrustServerCertificate = true,
                PersistSecurityInfo = false
            }.ConnectionString;

            _globalOverrideValues = new Dictionary<string, string>
            {
                ["globalSettings__baseServiceUri__vault"] = _context.Config.Url,
                ["globalSettings__sqlServer__connectionString"] = $"'{dbConnectionString}'",
                ["globalSettings__identityServer__certificatePassword"] = _context.Install?.IdentityCertPassword,
                ["globalSettings__internalIdentityKey"] = _context.Stub ? "RANDOM_IDENTITY_KEY" :
                    Helpers.SecureRandomString(64, alpha: true, numeric: true),
                ["globalSettings__oidcIdentityClientKey"] = _context.Stub ? "RANDOM_IDENTITY_KEY" :
                    Helpers.SecureRandomString(64, alpha: true, numeric: true),
                ["globalSettings__duo__aKey"] = _context.Stub ? "RANDOM_DUO_AKEY" :
                    Helpers.SecureRandomString(64, alpha: true, numeric: true),
                ["globalSettings__installation__id"] = _context.Install?.InstallationId.ToString(),
                ["globalSettings__installation__key"] = _context.Install?.InstallationKey,
                ["globalSettings__yubico__clientId"] = "REPLACE",
                ["globalSettings__yubico__key"] = "REPLACE",
                ["globalSettings__mail__replyToEmail"] = $"no-reply@{_context.Config.Domain}",
                ["globalSettings__mail__smtp__host"] = "REPLACE",
                ["globalSettings__mail__smtp__port"] = "587",
                ["globalSettings__mail__smtp__ssl"] = "false",
                ["globalSettings__mail__smtp__username"] = "REPLACE",
                ["globalSettings__mail__smtp__password"] = "REPLACE",
                ["globalSettings__disableUserRegistration"] = "false",
                ["globalSettings__hibpApiKey"] = "REPLACE",
                ["adminSettings__admins"] = string.Empty,
            };

            if (!_context.Config.PushNotifications)
            {
                _globalOverrideValues.Add("globalSettings__pushRelayBaseUri", "REPLACE");
            }

            _databaseOverrideValues = new Dictionary<string, string>
            {
                ["SA_PASSWORD"] = dbPassword,
            };
        }

        private void LoadExistingValues(IDictionary<string, string> _values, string file)
        {
            if (!File.Exists(file))
            {
                return;
            }

            var fileLines = File.ReadAllLines(file);
            foreach (var line in fileLines)
            {
                if (!line.Contains("="))
                {
                    continue;
                }

                var value = string.Empty;
                var lineParts = line.Split("=", 2);
                if (lineParts.Length < 1)
                {
                    continue;
                }

                if (lineParts.Length > 1)
                {
                    value = lineParts[1];
                }

                if (_values.ContainsKey(lineParts[0]))
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
            var template = Helpers.ReadTemplate("EnvironmentFile");

            Helpers.WriteLine(_context, "Building docker environment files.");
            Directory.CreateDirectory("/bitwarden/docker/");
            using (var sw = File.CreateText("/bitwarden/docker/global.env"))
            {
                sw.Write(template(new TemplateModel(_globalValues)));
            }
            Helpers.Exec("chmod 600 /bitwarden/docker/global.env");

            using (var sw = File.CreateText("/bitwarden/docker/database.env"))
            {
                sw.Write(template(new TemplateModel(_databaseValues)));
            }
            Helpers.Exec("chmod 600 /bitwarden/docker/database.env");

            Helpers.WriteLine(_context, "Building docker environment override files.");
            Directory.CreateDirectory("/bitwarden/env/");
            using (var sw = File.CreateText("/bitwarden/env/global.override.env"))
            {
                sw.Write(template(new TemplateModel(_globalOverrideValues)));
            }
            Helpers.Exec("chmod 600 /bitwarden/env/global.override.env");

            using (var sw = File.CreateText("/bitwarden/env/database.override.env"))
            {
                sw.Write(template(new TemplateModel(_databaseOverrideValues)));
            }
            Helpers.Exec("chmod 600 /bitwarden/env/database.override.env");

            // Empty uid env file. Only used on Linux hosts.
            if (!File.Exists("/bitwarden/env/uid.env"))
            {
                using (var sw = File.CreateText("/bitwarden/env/uid.env")) { }
            }
        }

        public class TemplateModel
        {
            public TemplateModel(IEnumerable<KeyValuePair<string, string>> variables)
            {
                Variables = variables.Select(v => new Kvp { Key = v.Key, Value = v.Value });
            }

            public IEnumerable<Kvp> Variables { get; set; }

            public class Kvp
            {
                public string Key { get; set; }
                public string Value { get; set; }
            }
        }
    }
}
