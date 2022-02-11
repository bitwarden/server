using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Xunit;

namespace Bit.Core.Test.Models
{
    public class CipherTests
    {
        [Theory]
        [InlineUserCipherAutoData]
        [InlineOrganizationCipherAutoData]
        public void Clone_CreatesExactCopy(Cipher cipher)
        {
            Assert.Equal(JsonSerializer.Serialize(cipher), JsonSerializer.Serialize(cipher.Clone()));
        }
    }
}
