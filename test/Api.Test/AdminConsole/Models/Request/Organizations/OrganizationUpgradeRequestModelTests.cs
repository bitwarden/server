using Bit.Api.AdminConsole.Models.Request.Organizations;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Models.Request.Organizations;

public class OrganizationUpgradeRequestModelTests
{
    [Fact]
    public void ToOrganizationUpgrade_MapsProrationDate()
    {
        var prorationDate = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var model = new OrganizationUpgradeRequestModel { ProrationDate = prorationDate };

        var result = model.ToOrganizationUpgrade();

        Assert.Equal(prorationDate, result.ProrationDate);
    }
}
