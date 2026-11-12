using Bit.Core.KeyManagement.KeyService;
using Bitwarden.Server.Sdk.Crypto;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Core.Test.KeyManagement.KeyService;

public class SdkKeyIdReaderTests
{
    // An RSA public key whose RFC 9679 thumbprint is 04fbcfa5...63aa, a vector of the SDK.
    private const string PublicKey =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArvcXfr5pCD6KhzXo7BWc" +
        "5Hdcbgp9U6hk0+wDYQBJ2yP8mlbd3GiN9JMFAtliE6BaTYLuxI9Mdk7XmDoKy63X" +
        "AuI8tUon5imL/792Wca3f3qrbZh9pOfPKWp7HkcByty1ZO8QPlEYUP24y4DzOfVd" +
        "LkdZfs9X5qKHiTxc+VklzTm3PSap4eORTQ/lP1GB10y0qJk5+44GRcSQSr3ku6ui" +
        "2re8AJ2GQhdnZz5oWaCb/kij5bQPBwBrIEBlgRdaeasVdR6wFJPJAQZxtqWo9MPK" +
        "eVDOkaQ3Qrryh+49S4rln3592/WeHYM5hO47DJr86ELcqcyCmksYas7xTqHfVfHS" +
        "XQIDAQAB";

    [Fact]
    public void PublicKey_IsTruncatedThumbprint()
    {
        var client = new ServiceCollection().AddServerSdkCrypto().BuildServiceProvider()
            .GetRequiredService<ICryptoParsingClient>();

        var keyId = new SdkKeyIdReader(client).ReadPublicKeyId(PublicKey);

        Assert.Equal("04fbcfa50c5805171304cc5b4794c25d", keyId);
    }

    [Fact]
    public void ResourceId_ReadsKeyIdAsGuid()
    {
        Assert.Equal(new Guid("04fbcfa5-0c58-0517-1304-cc5b4794c25d"),
            KeyIds.ToResourceId("04fbcfa50c5805171304cc5b4794c25d"));
    }
}
