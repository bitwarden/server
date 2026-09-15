using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Fido2NetLib.Objects;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bit.Core.Test.Auth.Models;

public class WebAuthnTwoFactorProviderTests
{
    // Shape Newtonsoft.Json produces when persisting a WebAuthn key (JsonHelpers.LegacySerialize) —
    // Id is standard Base64 (Convert.ToBase64String), not Fido2NetLib's Base64Url.
    private const string WebAuthnDataJson =
        "{\"Name\":\"Yubikey 5\",\"Descriptor\":{\"Id\":\"{ID}\",\"Type\":0,\"Transports\":null}," +
        "\"PublicKey\":\"AAAA\",\"UserHandle\":\"AAAA\",\"SignatureCounter\":0," +
        "\"RegDate\":\"2024-01-01T00:00:00\",\"Migrated\":false," +
        "\"AaGuid\":\"00000000-0000-0000-0000-000000000000\"}";

    [Fact]
    public void Ctor_DescriptorIdIsStandardBase64WithPlusOrSlash_IsReadBack()
    {
        // 32-byte ID whose standard Base64 contains '+'/'/' — ~74% of random IDs land here, so
        // this is the common case for stored keys, not an edge case. Fido2 v4 tightened
        // Base64UrlConverter to reject these characters; relaxed decoding restores v3 behavior.
        const string standardBase64Id = "RtCGgkCX5KOVz/9GaZxzxKHNEDQTW06jb4SlSt96DqA=";
        dynamic o = JObject.Parse(WebAuthnDataJson.Replace("{ID}", standardBase64Id));

        var data = new TwoFactorProvider.WebAuthnData(o);

        Assert.Equal(Convert.FromBase64String(standardBase64Id), data.Descriptor!.Id);
    }

    [Fact]
    public void Ctor_DescriptorIdAvoidsPlusAndSlash_IsReadBack()
    {
        // Same shape but an Id that avoids '+'/'/', so it decodes identically under both strict
        // and relaxed decoding. Guards against a "fix" that trades one alphabet for the other:
        // relaxed decoding must remain a superset, not a swap.
        const string standardBase64Id = "Xn2CI3j8mGhAOd7FRAn4xPqT2gOxlbxLieJsKcuPF0Q=";
        dynamic o = JObject.Parse(WebAuthnDataJson.Replace("{ID}", standardBase64Id));

        var data = new TwoFactorProvider.WebAuthnData(o);

        Assert.Equal(Convert.FromBase64String(standardBase64Id), data.Descriptor!.Id);
    }

    [Fact]
    public void Ctor_DescriptorIdIsBase64Url_IsReadBack()
    {
        // Base64Url form of the same 32 bytes as the first case ('_' for '/', no padding). Nothing
        // writes this shape today, but it is what a future System.Text.Json migration of
        // TwoFactorProviders would persist, so reads must keep accepting it.
        dynamic o = JObject.Parse(
            WebAuthnDataJson.Replace("{ID}", "RtCGgkCX5KOVz_9GaZxzxKHNEDQTW06jb4SlSt96DqA"));

        var data = new TwoFactorProvider.WebAuthnData(o);

        Assert.Equal(
            Convert.FromBase64String("RtCGgkCX5KOVz/9GaZxzxKHNEDQTW06jb4SlSt96DqA="),
            data.Descriptor!.Id);
    }

    // End-to-end guard that WebAuthnDataJson above matches what production actually persists:
    // store a key exactly the way CompleteTwoFactorWebAuthnRegistrationCommand does, then reload it
    // the way WebAuthnTokenProvider.LoadKeys does. This is the path that 500s when decoding is strict.
    [Fact]
    public void SetThenGetTwoFactorProviders_WebAuthnIdWithPlusOrSlash_RoundTrips()
    {
        var credentialId = Convert.FromBase64String("RtCGgkCX5KOVz/9GaZxzxKHNEDQTW06jb4SlSt96DqA=");
        var user = new User();
        user.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            [TwoFactorProviderType.WebAuthn] = new TwoFactorProvider
            {
                Enabled = true,
                MetaData = new Dictionary<string, object>
                {
                    ["Key1"] = new TwoFactorProvider.WebAuthnData
                    {
                        Name = "Yubikey 5",
                        Descriptor = new PublicKeyCredentialDescriptor(credentialId),
                    },
                },
            },
        });

        // Newtonsoft ignores System.Text.Json's [JsonConverter(typeof(Base64UrlConverter))], so it
        // writes byte[] as standard Base64 and the enum as an int. Asserted explicitly because the
        // whole problem is this asymmetry: Newtonsoft writes, System.Text.Json + Fido2 reads. If a
        // future change makes the write path emit Base64Url, this assertion should fail loudly
        // rather than let the compatibility shim quietly become dead code.
        Assert.Contains("\"Id\":\"RtCGgkCX5KOVz/9GaZxzxKHNEDQTW06jb4SlSt96DqA=\"", user.TwoFactorProviders);
        Assert.Contains("\"Type\":0", user.TwoFactorProviders);

        // SetTwoFactorProviders caches the live object graph in User._twoFactorProviders and
        // GetTwoFactorProviders short-circuits on it, so a fresh User is required to exercise the
        // JSON round-trip that every real request performs.
        var reloaded = new User { TwoFactorProviders = user.TwoFactorProviders };
        var metaData = reloaded.GetTwoFactorProviders()![TwoFactorProviderType.WebAuthn].MetaData["Key1"];

        // Deserialization leaves MetaData values as JObject, which is why LoadKeys hits the
        // ctor's dynamic-cast fallback rather than a typed WebAuthnData.
        Assert.IsType<JObject>(metaData);

        var reloadedKey = new TwoFactorProvider.WebAuthnData((dynamic)metaData);

        Assert.Equal(credentialId, reloadedKey.Descriptor!.Id);
        Assert.Equal("Yubikey 5", reloadedKey.Name);
    }
}
