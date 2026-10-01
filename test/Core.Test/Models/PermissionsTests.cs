using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Models;

public class PermissionsTests
{
    private static readonly string _exampleSerializedPermissions = string.Concat(
        "{",
        "\"accessEventLogs\": false,",
        "\"accessImportExport\": false,",
        "\"accessReports\": false,",
        "\"createNewCollections\": true,",
        "\"editAnyCollection\": true,",
        "\"deleteAnyCollection\": true,",
        "\"manageGroups\": false,",
        "\"managePolicies\": false,",
        "\"manageSso\": false,",
        "\"manageUsers\": false,",
        "\"manageResetPassword\": false,",
        "\"manageScim\": false,",
        "\"manageAccessRules\": false",
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
            ManageGroups = false,
            ManagePolicies = false,
            ManageSso = false,
            ManageUsers = false,
            ManageResetPassword = false,
            ManageScim = false,
            ManageAccessRules = false,
        };

        // minify expected json
        var expected = JsonSerializer.Serialize(permissions, JsonHelpers.CamelCase);

        var actual = JsonSerializer.Serialize(
            JsonHelpers.DeserializeOrNew<Permissions>(_exampleSerializedPermissions, JsonHelpers.CamelCase),
            JsonHelpers.CamelCase);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Includes_WhenOtherIsNull_ReturnsTrue()
    {
        Assert.True(new Permissions().Includes(null));
    }

    [Fact]
    public void Includes_WhenBothEmpty_ReturnsTrue()
    {
        Assert.True(new Permissions().Includes(new Permissions()));
    }

    [Fact]
    public void Includes_WhenEqual_ReturnsTrue()
    {
        var permissions = new Permissions { EditAnyCollection = true, ManageResetPassword = true };
        var other = new Permissions { EditAnyCollection = true, ManageResetPassword = true };

        Assert.True(permissions.Includes(other));
    }

    [Fact]
    public void Includes_WhenSuperset_ReturnsTrue()
    {
        var permissions = new Permissions { EditAnyCollection = true, ManageResetPassword = true };
        var other = new Permissions { EditAnyCollection = true };

        Assert.True(permissions.Includes(other));
    }

    [Fact]
    public void Includes_WhenMissingAPermission_ReturnsFalse()
    {
        var permissions = new Permissions { ManageResetPassword = true };
        var other = new Permissions { ManageResetPassword = true, AccessImportExport = true };

        Assert.False(permissions.Includes(other));
    }
}
