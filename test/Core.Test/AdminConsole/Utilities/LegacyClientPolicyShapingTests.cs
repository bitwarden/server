#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Utilities;

public class LegacyClientPolicyShapingTests
{
    private const string ObjectData = """{"restrictedTypes":[3,6]}""";
    private static readonly Version _minimum = new(Constants.PM35389RestrictedItemTypesDataMinimumVersion);

    [Theory]
    [InlineData(DeviceType.ChromeBrowser)]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.WindowsDesktop)]
    [InlineData(DeviceType.WindowsCLI)]
    public void CanParse_ClientAtOrAboveMinimum_True(DeviceType deviceType)
    {
        Assert.True(LegacyClientPolicyShaping.CanParseRestrictedItemTypesData(deviceType, _minimum));
        Assert.True(LegacyClientPolicyShaping.CanParseRestrictedItemTypesData(
            deviceType, new Version(_minimum.Major + 1, 0, 0)));
    }

    [Theory]
    [InlineData(DeviceType.ChromeBrowser)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.MacOsDesktop)]
    [InlineData(DeviceType.LinuxCLI)]
    public void CanParse_ClientBelowMinimum_False(DeviceType deviceType)
    {
        var below = new Version(_minimum.Major - 1, 12, 0);

        Assert.False(LegacyClientPolicyShaping.CanParseRestrictedItemTypesData(deviceType, below));
    }

    [Theory]
    [InlineData(DeviceType.ChromeBrowser)]
    [InlineData(DeviceType.WindowsDesktop)]
    [InlineData(DeviceType.UnknownBrowser)]
    public void CanParse_MissingVersion_False(DeviceType deviceType)
    {
        Assert.False(LegacyClientPolicyShaping.CanParseRestrictedItemTypesData(deviceType, null));
    }

    [Fact]
    public void CanParse_MissingDeviceTypeAndVersion_False()
    {
        Assert.False(LegacyClientPolicyShaping.CanParseRestrictedItemTypesData(null, null));
    }

    [Theory]
    [InlineData(DeviceType.Android, null)]
    [InlineData(DeviceType.iOS, "2020.1.0")]
    public void CanParse_Mobile_AlwaysTrue(DeviceType deviceType, string? version)
    {
        Assert.True(LegacyClientPolicyShaping.CanParseRestrictedItemTypesData(
            deviceType, version is null ? null : new Version(version)));
    }

    [Fact]
    public void ShapeForClient_LegacyClient_NullsRestrictedItemTypesData()
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);

        var result = Assert.Single(
            LegacyClientPolicyShaping.ShapeForClient([stored], DeviceType.ChromeBrowser, null));

        Assert.Null(result.Data);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(stored.OrganizationId, result.OrganizationId);
        Assert.Equal(stored.Type, result.Type);
        Assert.Equal(stored.Enabled, result.Enabled);
        Assert.Equal(stored.CreationDate, result.CreationDate);
        Assert.Equal(stored.RevisionDate, result.RevisionDate);
    }

    [Fact]
    public void ShapeForClient_LegacyClient_DoesNotMutateStoredEntity()
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);

        var result = LegacyClientPolicyShaping.ShapeForClient([stored], DeviceType.ChromeBrowser, null).Single();

        Assert.NotSame(stored, result);
        Assert.Equal(ObjectData, stored.Data);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShapeForClient_LegacyClient_PreservesEnabledState(bool enabled)
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);
        stored.Enabled = enabled;

        var result = LegacyClientPolicyShaping.ShapeForClient([stored], null, null).Single();

        Assert.Null(result.Data);
        Assert.Equal(enabled, result.Enabled);
    }

    [Fact]
    public void ShapeForClient_LegacyClient_NullData_ReturnsSameInstance()
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, null);

        Assert.Same(stored, LegacyClientPolicyShaping.ShapeForClient([stored], null, null).Single());
    }

    [Fact]
    public void ShapeForClient_LegacyClient_OtherPolicyTypes_ReturnedUnchanged()
    {
        var other = NewPolicy(PolicyType.MasterPassword, """{"minLength":12}""");

        Assert.Same(other, LegacyClientPolicyShaping.ShapeForClient([other], null, null).Single());
    }

    [Fact]
    public void ShapeForClient_SupportedClient_ReturnsPoliciesUnchanged()
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);

        var result = Assert.Single(
            LegacyClientPolicyShaping.ShapeForClient([stored], DeviceType.ChromeBrowser, _minimum));

        Assert.Same(stored, result);
        Assert.Equal(ObjectData, result.Data);
    }

    [Fact]
    public void ShapeForClient_Mobile_ReturnsPoliciesUnchanged()
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);

        Assert.Same(stored, LegacyClientPolicyShaping.ShapeForClient([stored], DeviceType.Android, null).Single());
    }

    [Fact]
    public void ShapeForClient_MixedPolicies_OnlyShapesRestrictedItemTypes()
    {
        var restricted = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);
        var other = NewPolicy(PolicyType.MasterPassword, """{"minLength":12}""");

        var result = LegacyClientPolicyShaping.ShapeForClient([restricted, other], null, null).ToList();

        Assert.Equal(2, result.Count);
        Assert.Null(result[0].Data);
        Assert.Same(other, result[1]);
    }

    [Fact]
    public void ShapeForBroadcast_RestrictedItemTypes_NullsDataWithoutMutating()
    {
        var stored = NewPolicy(PolicyType.RestrictedItemTypesPolicy, ObjectData);

        var result = LegacyClientPolicyShaping.ShapeForBroadcast(stored);

        Assert.Null(result.Data);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(ObjectData, stored.Data);
    }

    [Fact]
    public void ShapeForBroadcast_OtherPolicyTypes_ReturnedUnchanged()
    {
        var other = NewPolicy(PolicyType.MasterPassword, """{"minLength":12}""");

        Assert.Same(other, LegacyClientPolicyShaping.ShapeForBroadcast(other));
    }

    private static Policy NewPolicy(PolicyType type, string? data) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = Guid.NewGuid(),
        Type = type,
        Enabled = true,
        Data = data,
    };
}
