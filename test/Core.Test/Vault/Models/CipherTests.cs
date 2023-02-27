using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.CipherFixtures;
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
}
