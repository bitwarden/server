using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Bit.Core.Enums;
using Bit.Core.Models;
using DbUp.Engine;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;

namespace Bit.Migrator.DbScripts
{
    class ScriptMigrateU2FToWebAuthn : IScript
    {

        public string ProvideScript(Func<IDbCommand> commandFactory)
        {
            var cmd = commandFactory();
            cmd.CommandText = "SELECT Id, TwoFactorProviders FROM [dbo].[User] WHERE TwoFactorProviders IS NOT NULL";

            var users = new List<object>();

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetGuid(0);
                    var twoFactorProviders = reader.GetString(1);

                    if (string.IsNullOrWhiteSpace(twoFactorProviders))
                    {
                        continue;
                    }

                    var providers = JsonConvert.DeserializeObject<Dictionary<TwoFactorProviderType, TwoFactorProvider>>(twoFactorProviders);

                    if (!providers.ContainsKey(TwoFactorProviderType.U2f))
                    {
                        continue;
                    }

                    var u2fProvider = providers[TwoFactorProviderType.U2f];

                    if (!u2fProvider.Enabled || !HasProperMetaData(u2fProvider))
                    {
                        continue;
                    }

                    var u2fKeys = LoadKeys(u2fProvider);
                    var webAuthnKeys = u2fKeys.Select(key => (key.Item1, key.Item2.ToWebAuthnKey()));

                    var webAuthnProvider = new TwoFactorProvider
                    {
                        Enabled = true,
                        MetaData = webAuthnKeys.ToDictionary(x => x.Item1, x => (object)x.Item2)
                    };

                    providers[TwoFactorProviderType.WebAuthn] = webAuthnProvider;

                    users.Add(new User
                    {
                        Id = id,
                        TwoFactorProviders = JsonConvert.SerializeObject(providers),
                    });
                }
            }

            foreach (User user in users)
            {
                var command = commandFactory();

                command.CommandText = "UPDATE [dbo].[User] SET TwoFactorProviders = @twoFactorProviders WHERE Id = @id";
                var idParameter = command.CreateParameter();
                idParameter.ParameterName = "@id";
                idParameter.Value = user.Id;

                var twoFactorParameter = command.CreateParameter();
                twoFactorParameter.ParameterName = "@twoFactorProviders";
                twoFactorParameter.Value = user.TwoFactorProviders;

                command.Parameters.Add(idParameter);
                command.Parameters.Add(twoFactorParameter);

                command.ExecuteNonQuery();
            }

            return "";
        }

        private bool HasProperMetaData(TwoFactorProvider provider)
        {
            return (provider?.MetaData?.Count ?? 0) > 0;
        }

        private List<Tuple<string, TwoFactorProvider.U2fMetaData>> LoadKeys(TwoFactorProvider provider)
        {
            var keys = new List<Tuple<string, TwoFactorProvider.U2fMetaData>>();

            // Support up to 5 keys
            for (var i = 1; i <= 5; i++)
            {
                var keyName = $"Key{i}";
                if (provider.MetaData.ContainsKey(keyName))
                {
                    var key = new TwoFactorProvider.U2fMetaData((dynamic)provider.MetaData[keyName]);
                    if (!key?.Compromised ?? false)
                    {
                        keys.Add(new Tuple<string, TwoFactorProvider.U2fMetaData>(keyName, key));
                    }
                }
            }

            return keys;
        }

        private class User
        {
            public Guid Id { get; set; }
            public string TwoFactorProviders { get; set; }
        }
    }
}
