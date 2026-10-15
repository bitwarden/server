using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Entities;

public class OrganizationPartnershipTests
{
    [Fact]
    public void RegisteredReturnOrigins_DefaultsToEmpty()
    {
        Assert.Empty(new OrganizationPartnership().GetRegisteredReturnOrigins());
    }

    [Fact]
    public void RegisteredReturnOrigins_RoundTrips()
    {
        var partnership = new OrganizationPartnership();
        string[] origins = ["https://account.example.com", "https://app.example.com"];

        partnership.SetRegisteredReturnOrigins(origins);

        Assert.Equal(origins, partnership.GetRegisteredReturnOrigins());
    }

    [Fact]
    public void AllowedSponsoredPlanTypes_DefaultsToEmpty()
    {
        Assert.Empty(new OrganizationPartnership().GetAllowedSponsoredPlanTypes());
    }

    [Fact]
    public void AllowedSponsoredPlanTypes_RoundTrips()
    {
        var partnership = new OrganizationPartnership();

        partnership.SetAllowedSponsoredPlanTypes([SponsoredPlanType.Premium]);

        Assert.Equal([SponsoredPlanType.Premium], partnership.GetAllowedSponsoredPlanTypes());
    }

    [Fact]
    public void IsSponsoredPlanTypeAllowed_Listed_ReturnsTrue()
    {
        var partnership = new OrganizationPartnership();
        partnership.SetAllowedSponsoredPlanTypes([SponsoredPlanType.Premium]);

        Assert.True(partnership.IsSponsoredPlanTypeAllowed(SponsoredPlanType.Premium));
    }

    [Fact]
    public void IsSponsoredPlanTypeAllowed_NotListed_ReturnsFalse()
    {
        Assert.False(new OrganizationPartnership().IsSponsoredPlanTypeAllowed(SponsoredPlanType.Premium));
    }
}
