using Bit.Core.Utilities.Authorization;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class OrganizationResourceTests
{
    [Theory, BitAutoData]
    public void OrganizationResource_ConvertsToGuid(Guid id)
    {
        var organizationResource = new OrganizationResource(id);
        Guid result = organizationResource;
        Assert.Equal(0, result.CompareTo(id));
    }

    [Theory, BitAutoData]
    public void OrganizationResource_ConvertsToString(Guid id)
    {
        var organizationResource = new OrganizationResource(id);
        Assert.Equal(organizationResource.ToString(), id.ToString());
    }
}
