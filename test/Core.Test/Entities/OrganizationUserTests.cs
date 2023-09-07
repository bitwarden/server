using System.Text.Json;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Entities;

public class OrganizationUserTests
{
    [Theory]
    [BitAutoData]
    public void Clone_CreatesExactCopy(OrganizationUser organizationUser)
    {
        Assert.Equal(JsonSerializer.Serialize(organizationUser), JsonSerializer.Serialize(organizationUser.Clone()));
    }
}
