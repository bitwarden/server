using System;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Bit.Core.Test.Models
{
    public class PermissionsTests
    {
        private static readonly string _exampleSerializedPermissions = string.Concat(
            "{",
            "\"accessEventLogs\": false,",
            "\"accessImportExport\": false,",
            "\"accessReports\": false,",
            "\"manageAllCollections\": true,", // exists for backwards compatibility
            "\"createNewCollections\": true,",
            "\"editAnyCollection\": true,",
            "\"deleteAnyCollection\": true,",
            "\"manageAssignedCollections\": false,", // exists for backwards compatibility
            "\"editAssignedCollections\": false,",
            "\"deleteAssignedCollections\": false,",
            "\"manageGroups\": false,",
            "\"managePolicies\": false,",
            "\"manageSso\": false,",
            "\"manageUsers\": false,",
            "\"manageResetPassword\": false",
            "}");

        [Fact]
        public void Serialization_Success()
        {
            // minify expected json
            var expected = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(_exampleSerializedPermissions));

            DefaultContractResolver contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            var actual = JsonConvert.SerializeObject(
                CoreHelpers.LoadClassFromJsonData<Permissions>(_exampleSerializedPermissions), new JsonSerializerSettings
                {
                    ContractResolver = contractResolver,
                });

            Console.WriteLine(actual);
            Assert.Equal(expected, actual);
        }
    }
}
