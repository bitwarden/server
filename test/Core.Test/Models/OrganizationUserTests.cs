using AutoFixture.Xunit2;
using Bit.Core.Models.Table;
using Newtonsoft.Json;
using Xunit;

namespace Bit.Core.Test.Models
{
    public class OrganizationUserTests
    {
        [Theory, AutoData]
        public void Clone_CreatesExactCopy(OrganizationUser orgUser)
        {
            Assert.Equal(JsonConvert.SerializeObject(orgUser), JsonConvert.SerializeObject(orgUser.Clone()));
        }
    }
}
