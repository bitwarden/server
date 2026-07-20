using Bit.Core.Auth.Models;
using Fido2NetLib.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bit.Core.Test.Auth.Models;

public class WebAuthnProviderTests
{
    // User.SetTwoFactorProviders/GetTwoFactorProviders round-trip TwoFactorProvider.MetaData through
    // Newtonsoft.Json, so every real call to WebAuthnData(dynamic o) receives a Newtonsoft JObject rather
    // than an already-typed PublicKeyCredentialDescriptor. Newtonsoft doesn't honor Fido2NetLib's
    // System.Text.Json Base64UrlConverter attribute on Descriptor.Id, so old entries have that byte[]
    // written as standard Base64 instead of Base64Url. The direct `Descriptor = o.Descriptor` assignment
    // always fails for a JObject, so the constructor's catch block - which decodes Id itself - is what
    // actually runs in production, for both standard-Base64 and Base64Url encoded entries.
    private static readonly byte[] CredentialId = [0xfb, 0xff, 0xfe, 1, 2, 3, 250, 251, 252, 253];

    private static dynamic WebAuthnMetaDataEntry(string descriptorId)
    {
        var json = $$"""
            {
                "Name": "My Key",
                "Descriptor": { "Type": 0, "Id": "{{descriptorId}}" },
                "PublicKey": "AAAA",
                "UserHandle": "AAAA",
                "SignatureCounter": 0,
                "CredType": "public-key",
                "RegDate": "2021-01-01T00:00:00Z",
                "AaGuid": "00000000-0000-0000-0000-000000000000",
                "Migrated": false
            }
            """;
        return JsonConvert.DeserializeObject<JObject>(json);
    }

    [Fact]
    public void WebAuthnDataCtor_StandardBase64Id_DecodesDescriptorId()
    {
        var standardBase64Id = Convert.ToBase64String(CredentialId);
        Assert.Contains('+', standardBase64Id);
        Assert.Contains('/', standardBase64Id);
        Assert.EndsWith("=", standardBase64Id);

        var data = new TwoFactorProvider.WebAuthnData(WebAuthnMetaDataEntry(standardBase64Id));

        Assert.Equal(CredentialId, data.Descriptor.Id);
        Assert.Equal(PublicKeyCredentialType.PublicKey, data.Descriptor.Type);
    }

    [Fact]
    public void WebAuthnDataCtor_Base64UrlId_DecodesDescriptorId()
    {
        var base64UrlId = Convert.ToBase64String(CredentialId).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var data = new TwoFactorProvider.WebAuthnData(WebAuthnMetaDataEntry(base64UrlId));

        Assert.Equal(CredentialId, data.Descriptor.Id);
        Assert.Equal(PublicKeyCredentialType.PublicKey, data.Descriptor.Type);
    }
}
