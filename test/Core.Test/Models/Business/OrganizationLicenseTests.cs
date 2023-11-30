﻿using System.Text.Json;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Models.Business;

public class OrganizationLicenseTests
{
    /// <summary>
    /// Verifies that when the license file is loaded from disk using the current OrganizationLicense class,
    /// its hash does not change.
    /// This guards against the risk that properties added in later versions are accidentally included in the hash,
    /// or that a property is added without incrementing the version number.
    /// </summary>
    [Theory]
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion)] // Previous version (this property is 1 behind)
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion + 1)] // Current version
    public void OrganizationLicense_LoadFromDisk_HashDoesNotChange(int licenseVersion)
    {
        var license = OrganizationLicenseFileFixtures.GetVersion(licenseVersion);

        // Compare the hash loaded from the json to the hash generated by the current class
        Assert.Equal(Convert.FromBase64String(license.Hash), license.ComputeHash());
    }

    /// <summary>
    /// Verifies that when the license file is loaded from disk using the current OrganizationLicense class,
    /// it matches the Organization it was generated for.
    /// This guards against the risk that properties added in later versions are accidentally included in the validation
    /// </summary>
    [Theory]
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion)] // Previous version (this property is 1 behind)
    [BitAutoData(OrganizationLicense.CurrentLicenseFileVersion + 1)] // Current version
    public void OrganizationLicense_LoadedFromDisk_VerifyData_Passes(int licenseVersion)
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
        Assert.True(license.VerifyData(organization, globalSettings));
    }

    /// <summary>
    /// Helper used to generate a new json string to be added in OrganizationLicenseFileFixtures.
    /// Uncomment [Fact], run the test and copy the value of the `result` variable into OrganizationLicenseFileFixtures,
    /// following the instructions in that class.
    /// </summary>
    // [Fact]
    private void GenerateLicenseFileJsonString()
    {
        var organization = OrganizationLicenseFileFixtures.OrganizationFactory();
        var licensingService = Substitute.For<ILicensingService>();
        var installationId = new Guid(OrganizationLicenseFileFixtures.InstallationId);

        var license = new OrganizationLicense(organization, null, installationId, licensingService);

        var result = JsonSerializer.Serialize(license, JsonHelpers.Indented).Replace("\"", "'");
        // Put a break after this line, then copy and paste the value of `result` into OrganizationLicenseFileFixtures
    }
}
