using System.Text.Json;
using Bit.RustSDK;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Crypto;

/// <summary>
/// Locks the SDK-backed access-token key derivation against the sdk-internal reference vector
/// (auth/access_token.rs::can_decode_access_token) and proves the derived key round-trips a
/// {"encryptionKey":...} payload through the Rust SDK's type-2 EncString.
/// </summary>
public class AccessTokenCryptoTests
{
    // From sdk-internal can_decode_access_token: the 16-byte encryption key embedded in the token...
    private const string ReferenceEncryptionKeyB64 = "X8vbvA0bduihIDe/qrzIQQ==";

    // ...and the expected 64-byte enc||mac key it derives to.
    private const string ExpectedDerivedKeyB64 =
        "H9/oIRLtL9nGCQOVDjSMoEbJsjWXSOCb3qeyDt6ckzS3FhyboEDWyTP/CQfbIszNmAVg2ExFganG1FVFGXO/Jg==";

    [Fact]
    public void DeriveAccessTokenKey_MatchesSdkVector()
    {
        var derived = RustSdkService.DeriveAccessTokenKey(ReferenceEncryptionKeyB64);

        Assert.Equal(ExpectedDerivedKeyB64, derived);
    }

    [Fact]
    public void EncryptedPayload_RoundTripsUnderDerivedKey()
    {
        var derivedKeyB64 = RustSdkService.DeriveAccessTokenKey(ReferenceEncryptionKeyB64);

        const string organizationKeyB64 =
            "H9/oIRLtL9nGCQOVDjSMoEbJsjWXSOCb3qeyDt6ckzS3FhyboEDWyTP/CQfbIszNmAVg2ExFganG1FVFGXO/Jg==";
        var payload = JsonSerializer.Serialize(new { encryptionKey = organizationKeyB64 });

        var encrypted = RustSdkService.EncryptString(payload, derivedKeyB64);
        var decrypted = RustSdkService.DecryptString(encrypted, derivedKeyB64);

        Assert.StartsWith("2.", encrypted);
        using var document = JsonDocument.Parse(decrypted);
        Assert.Equal(organizationKeyB64, document.RootElement.GetProperty("encryptionKey").GetString());
    }
}
