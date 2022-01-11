using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Utilities;
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
            Assert.Equal(JsonHelpers.Serialize(cipher), JsonHelpers.Serialize(cipher.Clone()));
        }
    }
}
