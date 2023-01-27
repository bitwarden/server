using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Entities;

public class OrganizationTests
{
    private static readonly Dictionary<TwoFactorProviderType, TwoFactorProvider> _testConfig = new Dictionary<TwoFactorProviderType, TwoFactorProvider>()
    {
        [TwoFactorProviderType.OrganizationDuo] = new TwoFactorProvider
        {
            Enabled = true,
            MetaData = new Dictionary<string, object>
            {
                ["IKey"] = "IKey_value",
                ["SKey"] = "SKey_value",
                ["Host"] = "Host_value",
            },
        }
    };


    [Fact]
    public void SetTwoFactorProviders_Success()
    {
        var organization = new Organization();
        organization.SetTwoFactorProviders(_testConfig);

        using var jsonDocument = JsonDocument.Parse(organization.TwoFactorProviders);
        var root = jsonDocument.RootElement;
        Assert.False(true);
        var duo = AssertHelper.AssertJsonProperty(root, "6", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(duo, "Enabled", JsonValueKind.True);
        var duoMetaData = AssertHelper.AssertJsonProperty(duo, "MetaData", JsonValueKind.Object);
        var iKey = AssertHelper.AssertJsonProperty(duoMetaData, "IKey", JsonValueKind.String).GetString();
        Assert.Equal("IKey_value", iKey);
        var sKey = AssertHelper.AssertJsonProperty(duoMetaData, "SKey", JsonValueKind.String).GetString();
        Assert.Equal("SKey_value", sKey);
        var host = AssertHelper.AssertJsonProperty(duoMetaData, "Host", JsonValueKind.String).GetString();
        Assert.Equal("Host_value", host);
    }

    [Fact]
    public void GetTwoFactorProviders_Success()
    {
        // This is to get rid of the cached dictionary the SetTwoFactorProviders keeps so we can fully test the JSON reading
        // It intent is to mimic a storing of the entity in the database and it being read later
        var tempOrganization = new Organization();
        tempOrganization.SetTwoFactorProviders(_testConfig);
        var organization = new Organization
        {
            TwoFactorProviders = tempOrganization.TwoFactorProviders,
        };

        var twoFactorProviders = organization.GetTwoFactorProviders();

        var duo = Assert.Contains(TwoFactorProviderType.OrganizationDuo, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.True(duo.Enabled);
        Assert.NotNull(duo.MetaData);
        var iKey = Assert.Contains("IKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("IKey_value", iKey);
        var sKey = Assert.Contains("SKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("SKey_value", sKey);
        var host = Assert.Contains("Host", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("Host_value", host);
    }

    [Fact]
    public void GetTwoFactorProviders_SavedWithName_Success()
    {
        var organization = new Organization();
        // This should save items with the string name of the enum and we will validate that we can read
        // from that just incase some organizations have it saved that way.
        organization.TwoFactorProviders = JsonSerializer.Serialize(_testConfig);

        // Preliminary Asserts to make sure we are testing what we want to be testing
        using var jsonDocument = JsonDocument.Parse(organization.TwoFactorProviders);
        var root = jsonDocument.RootElement;
        // This means it saved the enum as its string name
        AssertHelper.AssertJsonProperty(root, "OrganizationDuo", JsonValueKind.Object);

        // Actual checks
        var twoFactorProviders = organization.GetTwoFactorProviders();

        var duo = Assert.Contains(TwoFactorProviderType.OrganizationDuo, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.True(duo.Enabled);
        Assert.NotNull(duo.MetaData);
        var iKey = Assert.Contains("IKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("IKey_value", iKey);
        var sKey = Assert.Contains("SKey", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("SKey_value", sKey);
        var host = Assert.Contains("Host", (IDictionary<string, object>)duo.MetaData);
        Assert.Equal("Host_value", host);
    }
}
