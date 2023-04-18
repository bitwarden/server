using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Core.Test.Entities;

public class UserTests
{
    //                              KB     MB     GB
    public const long Multiplier = 1024 * 1024 * 1024;

    [Fact]
    public void StorageBytesRemaining_HasMax_DoesNotHaveStorage_ReturnsMaxAsBytes()
    {
        short maxStorageGb = 1;

        var user = new User
        {
            MaxStorageGb = maxStorageGb,
            Storage = null,
        };

        var bytesRemaining = user.StorageBytesRemaining();

        Assert.Equal(bytesRemaining, maxStorageGb * Multiplier);
    }

    [Theory]
    [InlineData(2, 1 * Multiplier, 1 * Multiplier)]

    public void StorageBytesRemaining_HasMax_HasStorage_ReturnRemainingStorage(short maxStorageGb, long storageBytes, long expectedRemainingBytes)
    {
        var user = new User
        {
            MaxStorageGb = maxStorageGb,
            Storage = storageBytes,
        };

        var bytesRemaining = user.StorageBytesRemaining();

        Assert.Equal(expectedRemainingBytes, bytesRemaining);
    }

    private static readonly Dictionary<TwoFactorProviderType, TwoFactorProvider> _testTwoFactorConfig = new Dictionary<TwoFactorProviderType, TwoFactorProvider>
    {
        [TwoFactorProviderType.WebAuthn] = new TwoFactorProvider
        {
            Enabled = true,
            MetaData = new Dictionary<string, object>
            {
                ["Item"] = "thing",
            },
        },
        [TwoFactorProviderType.Email] = new TwoFactorProvider
        {
            Enabled = false,
            MetaData = new Dictionary<string, object>
            {
                ["Email"] = "test@email.com",
            },
        },
    };

    [Fact]
    public void SetTwoFactorProviders_Success()
    {
        var user = new User();
        user.SetTwoFactorProviders(_testTwoFactorConfig);

        using var jsonDocument = JsonDocument.Parse(user.TwoFactorProviders);
        var root = jsonDocument.RootElement;

        var webAuthn = AssertHelper.AssertJsonProperty(root, "7", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(webAuthn, "Enabled", JsonValueKind.True);
        var webMetaData = AssertHelper.AssertJsonProperty(webAuthn, "MetaData", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(webMetaData, "Item", JsonValueKind.String);

        var email = AssertHelper.AssertJsonProperty(root, "1", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(email, "Enabled", JsonValueKind.False);
        var emailMetaData = AssertHelper.AssertJsonProperty(email, "MetaData", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(emailMetaData, "Email", JsonValueKind.String);
    }

    [Fact]
    public void GetTwoFactorProviders_Success()
    {
        // This is to get rid of the cached dictionary the SetTwoFactorProviders keeps so we can fully test the JSON reading
        // It intent is to mimic a storing of the entity in the database and it being read later
        var tempUser = new User();
        tempUser.SetTwoFactorProviders(_testTwoFactorConfig);
        var user = new User
        {
            TwoFactorProviders = tempUser.TwoFactorProviders,
        };

        var twoFactorProviders = user.GetTwoFactorProviders();

        var webAuthn = Assert.Contains(TwoFactorProviderType.WebAuthn, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.True(webAuthn.Enabled);
        Assert.NotNull(webAuthn.MetaData);
        var webAuthnMetaDataItem = Assert.Contains("Item", (IDictionary<string, object>)webAuthn.MetaData);
        Assert.Equal("thing", webAuthnMetaDataItem);

        var email = Assert.Contains(TwoFactorProviderType.Email, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.False(email.Enabled);
        Assert.NotNull(email.MetaData);
        var emailMetaDataEmail = Assert.Contains("Email", (IDictionary<string, object>)email.MetaData);
        Assert.Equal("test@email.com", emailMetaDataEmail);
    }

    [Fact]
    public void GetTwoFactorProviders_SavedWithName_Success()
    {
        var user = new User();
        // This should save items with the string name of the enum and we will validate that we can read
        // from that just incase some users have it saved that way.
        user.TwoFactorProviders = JsonSerializer.Serialize(_testTwoFactorConfig);

        // Preliminary Asserts to make sure we are testing what we want to be testing
        using var jsonDocument = JsonDocument.Parse(user.TwoFactorProviders);
        var root = jsonDocument.RootElement;
        // This means it saved the enum as its string name
        AssertHelper.AssertJsonProperty(root, "WebAuthn", JsonValueKind.Object);
        AssertHelper.AssertJsonProperty(root, "Email", JsonValueKind.Object);

        // Actual checks
        var twoFactorProviders = user.GetTwoFactorProviders();

        var webAuthn = Assert.Contains(TwoFactorProviderType.WebAuthn, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.True(webAuthn.Enabled);
        Assert.NotNull(webAuthn.MetaData);
        var webAuthnMetaDataItem = Assert.Contains("Item", (IDictionary<string, object>)webAuthn.MetaData);
        Assert.Equal("thing", webAuthnMetaDataItem);

        var email = Assert.Contains(TwoFactorProviderType.Email, (IDictionary<TwoFactorProviderType, TwoFactorProvider>)twoFactorProviders);
        Assert.False(email.Enabled);
        Assert.NotNull(email.MetaData);
        var emailMetaDataEmail = Assert.Contains("Email", (IDictionary<string, object>)email.MetaData);
        Assert.Equal("test@email.com", emailMetaDataEmail);
    }
}
