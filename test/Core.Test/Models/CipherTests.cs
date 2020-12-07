using Bit.Core.Models.Table;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Newtonsoft.Json;
using Xunit;

namespace Core.Test.Models
{
    public class CipherTests
    {
        [Theory]
        [InlineUserCipherAutoData]
        [InlineOrganizationCipherAuthoData]
        public void Clone_CreatesExactCopy(Cipher cipher)
        {
            Assert.Equal(JsonConvert.SerializeObject(cipher), JsonConvert.SerializeObject(cipher.Clone()));
        }
    }
}
