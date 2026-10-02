using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Response.TwoFactor;

public class TwoFactorWebAuthnDetailsTests
{
    // WebAuthnData properties have internal setters not accessible from Api.Test, so
    // these tests seed the provider data via raw JSON — the same shape the User entity's
    // serializer produces in production. Mirrors the integration test's EnrollUserInWebAuthn.
    private const string WebAuthnKeyJson =
        "{\"Name\":\"{NAME}\",\"Descriptor\":{\"Id\":\"AAAA\",\"Type\":0,\"Transports\":null}," +
        "\"PublicKey\":\"AAAA\",\"UserHandle\":\"AAAA\",\"SignatureCounter\":0," +
        "\"RegDate\":\"2024-01-01T00:00:00\",\"Migrated\":{MIGRATED}," +
        "\"AaGuid\":\"00000000-0000-0000-0000-000000000000\"}";

    [Fact]
    public void Ctor_NullUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TwoFactorWebAuthnDetails(null!));
    }

    [Theory, BitAutoData]
    public void Ctor_NullTwoFactorProviders_DisabledFalse_KeysNull(User user)
    {
        user.TwoFactorProviders = null;

        var details = new TwoFactorWebAuthnDetails(user);

        Assert.False(details.Enabled);
        Assert.Null(details.Keys);
    }

    [Theory, BitAutoData]
    public void Ctor_ProviderWithEmptyMetaData_KeysEmpty(User user)
    {
        user.TwoFactorProviders = "{\"7\":{\"Enabled\":false,\"MetaData\":{}}}";

        var details = new TwoFactorWebAuthnDetails(user);

        Assert.False(details.Enabled);
        Assert.NotNull(details.Keys);
        Assert.Empty(details.Keys);
    }

    [Theory, BitAutoData]
    public void Ctor_ProviderWithKeys_PopulatesKeysCollection(User user)
    {
        var key0 = WebAuthnKeyJson.Replace("{NAME}", "Yubikey 5").Replace("{MIGRATED}", "false");
        var key1 = WebAuthnKeyJson.Replace("{NAME}", "Backup").Replace("{MIGRATED}", "true");
        user.TwoFactorProviders =
            $"{{\"7\":{{\"Enabled\":true,\"MetaData\":{{\"Key0\":{key0},\"Key1\":{key1}}}}}}}";

        var details = new TwoFactorWebAuthnDetails(user);

        Assert.True(details.Enabled);
        Assert.NotNull(details.Keys);
        var keys = details.Keys.ToList();
        Assert.Equal(2, keys.Count);
        Assert.Contains(keys, k => k.Id == 0 && k.Name == "Yubikey 5" && !k.Migrated);
        Assert.Contains(keys, k => k.Id == 1 && k.Name == "Backup" && k.Migrated);
    }

    [Theory, BitAutoData]
    public void Ctor_ProviderDisabledButHasKeys_EnabledFalse_KeysStillPopulated(User user)
    {
        var key0 = WebAuthnKeyJson.Replace("{NAME}", "OldKey").Replace("{MIGRATED}", "false");
        user.TwoFactorProviders =
            $"{{\"7\":{{\"Enabled\":false,\"MetaData\":{{\"Key0\":{key0}}}}}}}";

        var details = new TwoFactorWebAuthnDetails(user);

        Assert.False(details.Enabled);
        Assert.NotNull(details.Keys);
        Assert.Single(details.Keys);
        Assert.Equal("OldKey", details.Keys.First().Name);
    }
}
