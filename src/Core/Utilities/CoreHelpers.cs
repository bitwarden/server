using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Dapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Bit.Core.Utilities
{
    public static class CoreHelpers
    {
        private static readonly long _baseDateTicks = new DateTime(1900, 1, 1).Ticks;
        private static readonly DateTime _epoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Random _random = new Random();

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
            var table = new DataTable($"{columnName}Array", "dbo");
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
            var table = new DataTable("SelectionReadOnlyArray", "dbo");

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

        public static X509Certificate2 GetCertificate(string thumbprint)
        {
            // Clean possible garbage characters from thumbprint copy/paste
            // ref http://stackoverflow.com/questions/8448147/problems-with-x509store-certificates-find-findbythumbprint
            thumbprint = Regex.Replace(thumbprint, @"[^\da-fA-F]", string.Empty).ToUpper();

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

        public static long ToEpocMilliseconds(DateTime date)
        {
            return (long)Math.Round((date - _epoc).TotalMilliseconds, 0);
        }

        public static DateTime FromEpocMilliseconds(long milliseconds)
        {
            return _epoc.AddMilliseconds(milliseconds);
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

        public static bool FullFramework()
        {
#if NET461
            return true;
#else
            return false;
#endif
        }

        public static bool SettingHasValue(string setting)
        {
            if(string.IsNullOrWhiteSpace(setting) || setting.Equals("SECRET"))
            {
                return false;
            }

            return true;
        }
    }
}
