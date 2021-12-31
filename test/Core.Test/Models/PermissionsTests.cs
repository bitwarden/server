using System;
using System.Text.Json;
using AutoFixture.Xunit2;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Utilities;
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
            var permissions = new Permissions
            {
                AccessEventLogs = false,
                AccessImportExport = false,
                AccessReports = false,
                CreateNewCollections = true,
                EditAnyCollection = true,
                DeleteAnyCollection = true,
                EditAssignedCollections = false,
                DeleteAssignedCollections = false,
                ManageGroups = false,
                ManagePolicies = false,
                ManageSso = false,
                ManageUsers = false,
                ManageResetPassword = false,
            };

            // minify expected json
            var expected = JsonHelpers.Serialize(permissions, JsonHelpers.CamelCase);

            var actual = JsonHelpers.Serialize(
                JsonHelpers.DeserializeOrNew<Permissions>(_exampleSerializedPermissions, JsonHelpers.CamelCase),
                JsonHelpers.CamelCase);

            Assert.Equal(expected, actual);
        }
    }
}
