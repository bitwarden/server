using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Bit.Core.Utilities
{
    public static class CoreHelpers
    {
        private static readonly long _baseDateTicks = new DateTime(1900, 1, 1).Ticks;
        private static readonly DateTime _epoc = new DateTime(1970, 1, 1);

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
            table.Columns.Add(columnName, typeof(T));
            table.SetTypeName($"[dbo].[{columnName}Array]");

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

        public static X509Certificate2 GetCertificate(string thumbprint)
        {
            // Clean possible garbage characters from thumbprint copy/paste
            // ref http://stackoverflow.com/questions/8448147/problems-with-x509store-certificates-find-findbythumbprint
            thumbprint = Regex.Replace(thumbprint, @"[^\da-zA-z]", string.Empty).ToUpper();

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

        public static long ToEpocMilliseconds(DateTime date)
        {
            return (long)Math.Round((date - _epoc).TotalMilliseconds, 0);
        }

        public static DateTime FromEpocMilliseconds(long milliseconds)
        {
            return _epoc.AddMilliseconds(milliseconds);
        }
    }
}
