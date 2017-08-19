using System;
using System.Data.SqlClient;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Setup
{
    public static class Helpers
    {
        public static string SecureRandomString(int length, bool alpha = true, bool upper = true, bool lower = true,
            bool numeric = true, bool special = false)
        {
            return SecureRandomString(length, RandomStringCharacters(alpha, upper, lower, numeric, special));
        }

        // ref https://stackoverflow.com/a/8996788/1090359 with modifications
        public static string SecureRandomString(int length, string characters)
        {
            if(length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "length cannot be less than zero.");
            }

            if((characters?.Length ?? 0) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(characters), "characters invalid.");
            }

            const int byteSize = 0x100;
            if(byteSize < characters.Length)
            {
                throw new ArgumentException(
                    string.Format("{0} may contain no more than {1} characters.", nameof(characters), byteSize),
                    nameof(characters));
            }

            var outOfRangeStart = byteSize - (byteSize % characters.Length);
            using(var rng = RandomNumberGenerator.Create())
            {
                var sb = new StringBuilder();
                var buffer = new byte[128];
                while(sb.Length < length)
                {
                    rng.GetBytes(buffer);
                    for(var i = 0; i < buffer.Length && sb.Length < length; ++i)
                    {
                        // Divide the byte into charSet-sized groups. If the random value falls into the last group and the
                        // last group is too small to choose from the entire allowedCharSet, ignore the value in order to
                        // avoid biasing the result.
                        if(outOfRangeStart <= buffer[i])
                        {
                            continue;
                        }

                        sb.Append(characters[buffer[i] % characters.Length]);
                    }
                }

                return sb.ToString();
            }
        }

        private static string RandomStringCharacters(bool alpha, bool upper, bool lower, bool numeric, bool special)
        {
            var characters = string.Empty;
            if(alpha)
            {
                if(upper)
                {
                    characters += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                }

                if(lower)
                {
                    characters += "abcdefghijklmnopqrstuvwxyz";
                }
            }

            if(numeric)
            {
                characters += "0123456789";
            }

            if(special)
            {
                characters += "!@#$%^*&";
            }

            return characters;
        }

        public static string MakeSqlConnectionString(string server, string database, string username, string password)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = $"tcp:{server},1433",
                InitialCatalog = database,
                UserID = username,
                Password = password,
                MultipleActiveResultSets = false,
                Encrypt = true,
                ConnectTimeout = 30,
                TrustServerCertificate = true,
                PersistSecurityInfo = false
            };
            return builder.ConnectionString;
        }

        public static string GetDatabasePasswordFronEnvFile()
        {
            if(!File.Exists("/bitwarden/docker/mssql.override.env"))
            {
                return null;
            }

            var lines = File.ReadAllLines("/bitwarden/docker/mssql.override.env");
            foreach(var line in lines)
            {
                if(line.StartsWith("SA_PASSWORD="))
                {
                    return line.Split(new char[] { '=' }, 2)[1];
                }
            }

            return null;
        }
    }
}
