using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Newtonsoft.Json;
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
            Assert.Equal(JsonConvert.SerializeObject(cipher), JsonConvert.SerializeObject(cipher.Clone()));
        }
    }
}
