using System.Security.Claims;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Models.Business;

public class OrganizationLicenseTests
{

    /// <summary>
    /// Verifies that when the license file is loaded from disk using the current OrganizationLicense class,
    /// it matches the Organization it was generated for.
    /// This guards against the risk that properties added in later versions are accidentally included in the validation
    /// </summary>
    [Theory]
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion)] // Previous version (this property is 1 behind)
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion + 1)] // Current version
    public void OrganizationLicense_LoadedFromDisk_VerifyData_Passes(int licenseVersion, ClaimsPrincipal claimsPrincipal)
    {
        var license = OrganizationLicenseFileFixtures.GetVersion(licenseVersion);

        // These licenses will naturally expire over time, but we still want them to be able to test
        license.Expires = DateTime.MaxValue;

        var organization = OrganizationLicenseFileFixtures.OrganizationFactory();
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Returns(new GlobalSettings.InstallationSettings
        {
            Id = new Guid(OrganizationLicenseFileFixtures.InstallationId)
        });
        Assert.True(license.VerifyData(organization, claimsPrincipal, globalSettings));
    }
}
