using System.Security.Claims;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Services;

public class OrganizationFactoryTests
{
    [Theory, BitAutoData]
    public void Create_WithClaimsPrincipal_MapsPamSeats(User owner, string publicKey, string privateKey)
    {
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationLicenseConstants.PamSeats, "25")
        ]));

        var organization = OrganizationFactory.Create(owner, claimsPrincipal, publicKey, privateKey);

        Assert.Equal(25, organization.PamSeats);
    }

    [Theory, BitAutoData]
    public void Create_WithClaimsPrincipal_NoPamSeatsClaim_PamSeatsNull(User owner, string publicKey, string privateKey)
    {
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OrganizationLicenseConstants.UsePam, "true")
        ]));

        var organization = OrganizationFactory.Create(owner, claimsPrincipal, publicKey, privateKey);

        Assert.True(organization.UsePam);
        Assert.Null(organization.PamSeats);
    }

    [Theory, BitAutoData]
    public void Create_WithLicense_IgnoresPamSeats(User owner, string publicKey, string privateKey)
    {
        var license = new OrganizationLicense { PamSeats = 25 };

        var organization = OrganizationFactory.Create(owner, license, publicKey, privateKey);

        Assert.Null(organization.PamSeats);
    }
}
