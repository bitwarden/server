using Bit.Core.Models.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using System.Globalization;
using System.Web;
using Microsoft.AspNetCore.DataProtection;
using Bit.Core.Enums;

namespace Bit.Core.Utilities
{
    public static class CoreHelpers
    {
        private static readonly long _baseDateTicks = new DateTime(1900, 1, 1).Ticks;
        private static readonly DateTime _epoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime _max = new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Random _random = new Random();
        private static string _version;
        private static readonly string _qwertyDvorakMap = "-=qwertyuiop[]asdfghjkl;'zxcvbnm,./_+QWERTYUIO" +
            "P{}ASDFGHJKL:\"ZXCVBNM<>?";
        private static readonly string _dvorakMap = "[]',.pyfgcrl/=aoeuidhtns-;qjkxbmwvz{}\"<>PYFGC" +
            "RL?+AOEUIDHTNS_:QJKXBMWVZ";
        private static readonly string _qwertyColemakMap = "qwertyuiopasdfghjkl;zxcvbnmQWERTYUIOPASDFGHJKL:ZXCVBNM";
        private static readonly string _colemakMap = "qwfpgjluy;arstdhneiozxcvbkmQWFPGJLUY:ARSTDHNEIOZXCVBKM";

        /// <summary>
        /// Generate sequential Guid for Sql Server.
        /// ref: https://github.com/nhibernate/nhibernate-core/blob/master/src/NHibernate/Id/GuidCombGenerator.cs
        /// </summary>
        /// <returns>A comb Guid.</returns>
        public static Guid GenerateComb()
        {
            var guidArray = Guid.NewGuid().ToByteArray();

            var now = DateTime.UtcNow;

            // Get the days and milliseconds which will be used to build the byte string 
            var days = new TimeSpan(now.Ticks - _baseDateTicks);
            var msecs = now.TimeOfDay;

            // Convert to a byte array 
            // Note that SQL Server is accurate to 1/300th of a millisecond so we divide by 3.333333 
            var daysArray = BitConverter.GetBytes(days.Days);
            var msecsArray = BitConverter.GetBytes((long)(msecs.TotalMilliseconds / 3.333333));

            // Reverse the bytes to match SQL Servers ordering 
            Array.Reverse(daysArray);
            Array.Reverse(msecsArray);

            // Copy the bytes into the guid 
            Array.Copy(daysArray, daysArray.Length - 2, guidArray, guidArray.Length - 6, 2);
            Array.Copy(msecsArray, msecsArray.Length - 4, guidArray, guidArray.Length - 4, 4);

            return new Guid(guidArray);
        }

        public static DataTable ToGuidIdArrayTVP(this IEnumerable<Guid> ids)
        {
            return ids.ToArrayTVP("GuidId");
        }

        public static DataTable ToArrayTVP<T>(this IEnumerable<T> values, string columnName)
        {
            var table = new DataTable();
            table.SetTypeName($"[dbo].[{columnName}Array]");
            table.Columns.Add(columnName, typeof(T));

            if(values != null)
            {
                foreach(var value in values)
                {
                    table.Rows.Add(value);
                }
            }

            return table;
        }

        public static DataTable ToArrayTVP(this IEnumerable<SelectionReadOnly> values)
        {
            var table = new DataTable();
            table.SetTypeName("[dbo].[SelectionReadOnlyArray]");

            var idColumn = new DataColumn("Id", typeof(Guid));
            table.Columns.Add(idColumn);
            var readOnlyColumn = new DataColumn("ReadOnly", typeof(bool));
            table.Columns.Add(readOnlyColumn);

            if(values != null)
            {
                foreach(var value in values)
                {
                    var row = table.NewRow();
                    row[idColumn] = value.Id;
                    row[readOnlyColumn] = value.ReadOnly;
                    table.Rows.Add(row);
                }
            }

            return table;
        }

        public static string CleanCertificateThumbprint(string thumbprint)
        {
            // Clean possible garbage characters from thumbprint copy/paste
            // ref http://stackoverflow.com/questions/8448147/problems-with-x509store-certificates-find-findbythumbprint
            return Regex.Replace(thumbprint, @"[^\da-fA-F]", string.Empty).ToUpper();
        }

        public static X509Certificate2 GetCertificate(string thumbprint)
        {
            thumbprint = CleanCertificateThumbprint(thumbprint);

            X509Certificate2 cert = null;
            var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certStore.Open(OpenFlags.ReadOnly);
            var certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if(certCollection.Count > 0)
            {
                cert = certCollection[0];
            }

            certStore.Close();
            return cert;
        }

        public static X509Certificate2 GetCertificate(string file, string password)
        {
            return new X509Certificate2(file, password);
        }

        public static X509Certificate2 GetEmbeddedCertificate(string file, string password)
        {
            var assembly = typeof(CoreHelpers).GetTypeInfo().Assembly;
            using(var s = assembly.GetManifestResourceStream($"Bit.Core.{file}"))
            using(var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return new X509Certificate2(ms.ToArray(), password);
            }
        }

        public static long ToEpocMilliseconds(DateTime date)
        {
            return (long)Math.Round((date - _epoc).TotalMilliseconds, 0);
        }

        public static DateTime FromEpocMilliseconds(long milliseconds)
        {
            return _epoc.AddMilliseconds(milliseconds);
        }

        public static long ToEpocSeconds(DateTime date)
        {
            return (long)Math.Round((date - _epoc).TotalSeconds, 0);
        }

        public static DateTime FromEpocSeconds(long seconds)
        {
            return _epoc.AddSeconds(seconds);
        }

        public static string U2fAppIdUrl(GlobalSettings globalSettings)
        {
            return string.Concat(globalSettings.BaseServiceUri.Vault, "/app-id.json");
        }

        public static string RandomString(int length, bool alpha = true, bool upper = true, bool lower = true,
            bool numeric = true, bool special = false)
        {
            return RandomString(length, RandomStringCharacters(alpha, upper, lower, numeric, special));
        }

        public static string RandomString(int length, string characters)
        {
            return new string(Enumerable.Repeat(characters, length).Select(s => s[_random.Next(s.Length)]).ToArray());
        }

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

        // ref: https://stackoverflow.com/a/11124118/1090359
        // Returns the human-readable file size for an arbitrary 64-bit file size .
        // The format is "0.## XB", ex: "4.2 KB" or "1.43 GB"
        public static string ReadableBytesSize(long size)
        {
            // Get absolute value
            var absoluteSize = (size < 0 ? -size : size);

            // Determine the suffix and readable value
            string suffix;
            double readable;
            if(absoluteSize >= 0x40000000) // 1 Gigabyte
            {
                suffix = "GB";
                readable = (size >> 20);
            }
            else if(absoluteSize >= 0x100000) // 1 Megabyte
            {
                suffix = "MB";
                readable = (size >> 10);
            }
            else if(absoluteSize >= 0x400) // 1 Kilobyte
            {
                suffix = "KB";
                readable = size;
            }
            else
            {
                return absoluteSize.ToString("0 Bytes"); // Byte
            }

            // Divide by 1024 to get fractional value
            readable = (readable / 1024);

            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }

        public static T CloneObject<T>(T obj)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj));
        }

        public static bool SettingHasValue(string setting)
        {
            var normalizedSetting = setting?.ToLowerInvariant();
            return !string.IsNullOrWhiteSpace(normalizedSetting) && !normalizedSetting.Equals("secret") &&
                !normalizedSetting.Equals("replace");
        }

        public static string Base64EncodeString(string input)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }

        public static string Base64DecodeString(string input)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(input));
        }

        public static string Base64UrlEncodeString(string input)
        {
            return Base64UrlEncode(Encoding.UTF8.GetBytes(input));
        }

        public static string Base64UrlDecodeString(string input)
        {
            return Encoding.UTF8.GetString(Base64UrlDecode(input));
        }

        public static string Base64UrlEncode(byte[] input)
        {
            var output = Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", string.Empty);
            return output;
        }

        public static byte[] Base64UrlDecode(string input)
        {
            var output = input;
            // 62nd char of encoding
            output = output.Replace('-', '+');
            // 63rd char of encoding
            output = output.Replace('_', '/');
            // Pad with trailing '='s
            switch(output.Length % 4)
            {
                case 0:
                    // No pad chars in this case
                    break;
                case 2:
                    // Two pad chars
                    output += "=="; break;
                case 3:
                    // One pad char
                    output += "="; break;
                default:
                    throw new InvalidOperationException("Illegal base64url string!");
            }

            // Standard base64 decoder
            return Convert.FromBase64String(output);
        }

        public static string FormatLicenseSignatureValue(object val)
        {
            if(val == null)
            {
                return string.Empty;
            }

            if(val.GetType() == typeof(DateTime))
            {
                return ToEpocSeconds((DateTime)val).ToString();
            }

            if(val.GetType() == typeof(bool))
            {
                return val.ToString().ToLowerInvariant();
            }

            return val.ToString();
        }

        public static string GetVersion()
        {
            if(string.IsNullOrWhiteSpace(_version))
            {
                _version = Assembly.GetEntryAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    .InformationalVersion;
            }

            return _version;
        }

        public static string Dvorak2Qwerty(string value)
        {
            return Other2Qwerty(value, _dvorakMap, _qwertyDvorakMap);
        }

        public static string Colemak2Qwerty(string value)
        {
            return Other2Qwerty(value, _colemakMap, _qwertyColemakMap);
        }

        private static string Other2Qwerty(string value, string otherMap, string qwertyMap)
        {
            var sb = new StringBuilder();
            foreach(var c in value)
            {
                sb.Append(otherMap.IndexOf(c) > -1 ? qwertyMap[otherMap.IndexOf(c)] : c);
            }
            return sb.ToString();
        }

        public static string SanitizeForEmail(string value)
        {
            return value.Replace("@", "[at]")
                .Replace("http://", string.Empty)
                .Replace("https://", string.Empty);
        }

        public static string DateTimeToTableStorageKey(DateTime? date = null)
        {
            if(date.HasValue)
            {
                date = date.Value.ToUniversalTime();
            }
            else
            {
                date = DateTime.UtcNow;
            }

            return _max.Subtract(date.Value).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        }

        // ref: https://stackoverflow.com/a/27545010/1090359
        public static Uri ExtendQuery(Uri uri, IDictionary<string, string> values)
        {
            var baseUri = uri.ToString();
            var queryString = string.Empty;
            if(baseUri.Contains("?"))
            {
                var urlSplit = baseUri.Split('?');
                baseUri = urlSplit[0];
                queryString = urlSplit.Length > 1 ? urlSplit[1] : string.Empty;
            }

            var queryCollection = HttpUtility.ParseQueryString(queryString);
            foreach(var kvp in values ?? new Dictionary<string, string>())
            {
                queryCollection[kvp.Key] = kvp.Value;
            }

            var uriKind = uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative;
            if(queryCollection.Count == 0)
            {
                return new Uri(baseUri, uriKind);
            }
            return new Uri(string.Format("{0}?{1}", baseUri, queryCollection), uriKind);
        }

        public static bool UserInviteTokenIsValid(IDataProtector protector, string token,
            string userEmail, Guid orgUserId)
        {
            var invalid = true;
            try
            {
                var unprotectedData = protector.Unprotect(token);
                var dataParts = unprotectedData.Split(' ');
                if(dataParts.Length == 4 && dataParts[0] == "OrganizationUserInvite" &&
                    new Guid(dataParts[1]) == orgUserId &&
                    dataParts[2].Equals(userEmail, StringComparison.InvariantCultureIgnoreCase))
                {
                    var creationTime = FromEpocMilliseconds(Convert.ToInt64(dataParts[3]));
                    invalid = creationTime.AddDays(5) < DateTime.UtcNow;
                }
            }
            catch
            {
                invalid = true;
            }

            return !invalid;
        }

        public static string CustomProviderName(TwoFactorProviderType type)
        {
            return string.Concat("Custom_", type.ToString());
        }
    }
}
