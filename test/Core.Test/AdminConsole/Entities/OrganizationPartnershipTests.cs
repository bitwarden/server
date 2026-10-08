using Bit.Core.AdminConsole.Entities;
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
}
