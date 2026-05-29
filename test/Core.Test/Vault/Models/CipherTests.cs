using System.Text.Json;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Models;

public class CipherTests
{
    [Theory]
    [UserCipherCustomize]
    [BitAutoData]
    public void Clone_UserCipher_CreatesExactCopy(Cipher cipher)
    {
        Assert.Equal(JsonSerializer.Serialize(cipher), JsonSerializer.Serialize(cipher.Clone()));
    }

    [Theory]
    [OrganizationCipherCustomize]
    [BitAutoData]
    public void Clone_OrganizationCipher_CreatesExactCopy(Cipher cipher)
    {
        Assert.Equal(JsonSerializer.Serialize(cipher), JsonSerializer.Serialize(cipher.Clone()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"Name\":\"x\"}")]
    [InlineData("   { \"Name\": \"x\" }")]
    public void IsDataBlobEncrypted_LegacyOrEmpty_ReturnsFalse(string? data)
    {
        var cipher = new Cipher { Data = data! };
        Assert.False(cipher.IsDataBlobEncrypted());
    }

    [Theory]
    [InlineData("2.iv|ct|mac")]
    [InlineData("plain string")]
    [InlineData("   2.iv|ct|mac")]
    public void IsDataBlobEncrypted_OpaqueData_ReturnsTrue(string data)
    {
        var cipher = new Cipher { Data = data };
        Assert.True(cipher.IsDataBlobEncrypted());
    }
}
