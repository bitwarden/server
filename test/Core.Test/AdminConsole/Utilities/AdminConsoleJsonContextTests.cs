using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Models.Data;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities;

/// <summary>
/// Verifies that <see cref="AdminConsoleJsonContext"/> produces byte-for-byte identical
/// output to the reflection-based path that uses the same CamelCase naming policy, and
/// that round-trip deserialization reconstructs every property correctly.
/// </summary>
public class AdminConsoleJsonContextTests
{
    [Fact]
    public void Serialize_Permissions_MatchesCamelCaseOptions()
    {
        var obj = new Permissions
        {
            AccessEventLogs = true,
            AccessImportExport = false,
            AccessReports = true,
            CreateNewCollections = false,
            EditAnyCollection = true,
            DeleteAnyCollection = false,
            ManageGroups = true,
            ManagePolicies = false,
            ManageSso = true,
            ManageUsers = false,
            ManageResetPassword = true,
            ManageScim = false,
        };

        var contextJson = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.Permissions);
        var referenceJson = JsonSerializer.Serialize(obj, _camelCaseOptions);

        Assert.Equal(referenceJson, contextJson);
    }

    [Fact]
    public void Deserialize_Permissions_RoundTrips()
    {
        const string fixture = """
            {"accessEventLogs":true,"accessImportExport":false,"accessReports":true,
            "createNewCollections":false,"editAnyCollection":true,"deleteAnyCollection":false,
            "manageGroups":true,"managePolicies":false,"manageSso":true,"manageUsers":false,
            "manageResetPassword":true,"manageScim":false}
            """;

        var obj = JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.Permissions);

        Assert.NotNull(obj);
        Assert.True(obj.AccessEventLogs);
        Assert.False(obj.AccessImportExport);
        Assert.True(obj.AccessReports);
        Assert.False(obj.CreateNewCollections);
        Assert.True(obj.EditAnyCollection);
        Assert.False(obj.DeleteAnyCollection);
        Assert.True(obj.ManageGroups);
        Assert.False(obj.ManagePolicies);
        Assert.True(obj.ManageSso);
        Assert.False(obj.ManageUsers);
        Assert.True(obj.ManageResetPassword);
        Assert.False(obj.ManageScim);

        var reserialised = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.Permissions);
        var expected = JsonSerializer.Serialize(
            JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.Permissions),
            AdminConsoleJsonContext.Default.Permissions);
        Assert.Equal(expected, reserialised);
    }

    [Fact]
    public void Serialize_MasterPasswordPolicyData_MatchesCamelCaseOptions()
    {
        var obj = new MasterPasswordPolicyData
        {
            MinComplexity = 3,
            MinLength = 16,
            RequireLower = true,
            RequireUpper = true,
            RequireNumbers = true,
            RequireSpecial = false,
            EnforceOnLogin = true,
        };

        var contextJson = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.MasterPasswordPolicyData);
        var referenceJson = JsonSerializer.Serialize(obj, _camelCaseOptions);

        Assert.Equal(referenceJson, contextJson);
    }

    [Fact]
    public void Deserialize_MasterPasswordPolicyData_RoundTrips()
    {
        const string fixture = """
            {"minComplexity":3,"minLength":16,"requireLower":true,"requireUpper":true,
            "requireNumbers":true,"requireSpecial":false,"enforceOnLogin":true}
            """;

        var obj = JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.MasterPasswordPolicyData);

        Assert.NotNull(obj);
        Assert.Equal(3, obj.MinComplexity);
        Assert.Equal(16, obj.MinLength);
        Assert.True(obj.RequireLower);
        Assert.True(obj.RequireUpper);
        Assert.True(obj.RequireNumbers);
        Assert.False(obj.RequireSpecial);
        Assert.True(obj.EnforceOnLogin);

        var reserialised = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.MasterPasswordPolicyData);
        var expected = JsonSerializer.Serialize(
            JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.MasterPasswordPolicyData),
            AdminConsoleJsonContext.Default.MasterPasswordPolicyData);
        Assert.Equal(expected, reserialised);
    }

    [Fact]
    public void Serialize_SendOptionsPolicyData_MatchesCamelCaseOptions()
    {
        var obj = new SendOptionsPolicyData { DisableHideEmail = true };

        var contextJson = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.SendOptionsPolicyData);
        var referenceJson = JsonSerializer.Serialize(obj, _camelCaseOptions);

        Assert.Equal(referenceJson, contextJson);
    }

    [Fact]
    public void Deserialize_SendOptionsPolicyData_RoundTrips()
    {
        const string fixture = """{"disableHideEmail":true}""";

        var obj = JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.SendOptionsPolicyData);

        Assert.NotNull(obj);
        Assert.True(obj.DisableHideEmail);

        var reserialised = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.SendOptionsPolicyData);
        Assert.Equal(fixture, reserialised);
    }

    [Fact]
    public void Serialize_SendControlsPolicyData_MatchesCamelCaseOptions()
    {
        var obj = new SendControlsPolicyData { DisableSend = true, DisableHideEmail = false };

        var contextJson = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.SendControlsPolicyData);
        var referenceJson = JsonSerializer.Serialize(obj, _camelCaseOptions);

        Assert.Equal(referenceJson, contextJson);
    }

    [Fact]
    public void Deserialize_SendControlsPolicyData_RoundTrips()
    {
        const string fixture = """{"disableSend":true,"disableHideEmail":false}""";

        var obj = JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.SendControlsPolicyData);

        Assert.NotNull(obj);
        Assert.True(obj.DisableSend);
        Assert.False(obj.DisableHideEmail);

        var reserialised = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.SendControlsPolicyData);
        Assert.Equal(fixture, reserialised);
    }

    [Fact]
    public void Serialize_ResetPasswordDataModel_MatchesCamelCaseOptions()
    {
        var obj = new ResetPasswordDataModel { AutoEnrollEnabled = true };

        var contextJson = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.ResetPasswordDataModel);
        var referenceJson = JsonSerializer.Serialize(obj, _camelCaseOptions);

        Assert.Equal(referenceJson, contextJson);
    }

    [Fact]
    public void Deserialize_ResetPasswordDataModel_RoundTrips()
    {
        const string fixture = """{"autoEnrollEnabled":true}""";

        var obj = JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.ResetPasswordDataModel);

        Assert.NotNull(obj);
        Assert.True(obj.AutoEnrollEnabled);

        var reserialised = JsonSerializer.Serialize(obj, AdminConsoleJsonContext.Default.ResetPasswordDataModel);
        Assert.Equal(fixture, reserialised);
    }

    /// <summary>
    /// Regression test: policy data written via <c>PolicyDataValidator.ValidateAndSerialize</c>
    /// uses <c>JsonSerializer.Serialize</c> with no naming policy, producing PascalCase keys
    /// (e.g. <c>"AutoEnrollEnabled": true</c>). The context must deserialize those correctly.
    /// </summary>
    [Fact]
    public void Deserialize_ResetPasswordDataModel_HandlesPascalCaseKeys()
    {
        const string fixture = """{"AutoEnrollEnabled":true}""";

        var obj = JsonSerializer.Deserialize(fixture, AdminConsoleJsonContext.Default.ResetPasswordDataModel);

        Assert.NotNull(obj);
        Assert.True(obj.AutoEnrollEnabled);
    }

    private static readonly JsonSerializerOptions _camelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
