using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.OrganizationFeatures.OrganizationLicenses;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationLicenses;

[SutProviderCustomize]
public class UpdateOrganizationLicenseCommandTests
{
    private static string LicenseDirectory => Path.GetDirectoryName(OrganizationLicenseDirectory.Value);
    private static Lazy<string> OrganizationLicenseDirectory => new(() =>
    {
        // Create a temporary directory to write the license file to
        var directory = Path.Combine(Path.GetTempPath(), "bitwarden/");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    });

    [Theory, BitAutoData]
    public async Task UpdateLicenseAsync_UpdatesLicenseFileAndOrganization(
        SelfHostedOrganizationDetails selfHostedOrg,
        OrganizationLicense license,
        SutProvider<UpdateOrganizationLicenseCommand> sutProvider)
    {
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        globalSettings.LicenseDirectory = LicenseDirectory;
        globalSettings.SelfHosted = true;

        // Passing values for OrganizationLicense.CanUse
        // NSubstitute cannot override non-virtual members so we have to ensure the real method passes
        license.Enabled = true;
        license.Issued = DateTime.Now.AddDays(-1);
        license.Expires = DateTime.Now.AddDays(1);
        license.Version = OrganizationLicense.CurrentLicenseFileVersion;
        license.InstallationId = globalSettings.Installation.Id;
        license.LicenseType = LicenseType.Organization;
        sutProvider.GetDependency<ILicensingService>().VerifyLicense(license).Returns(true);

        // Passing values for SelfHostedOrganizationDetails.CanUseLicense
        // NSubstitute cannot override non-virtual members so we have to ensure the real method passes
        license.Seats = null;
        license.MaxCollections = null;
        license.UseGroups = true;
        license.UsePolicies = true;
        license.UseSso = true;
        license.UseKeyConnector = true;
        license.UseScim = true;
        license.UseCustomPermissions = true;
        license.UseResetPassword = true;

        try
        {
            await sutProvider.Sut.UpdateLicenseAsync(selfHostedOrg, license, null);

            // Assertion: should have saved the license file to disk
            var filePath = Path.Combine(LicenseDirectory, "organization", $"{selfHostedOrg.Id}.json");
            await using var fs = File.OpenRead(filePath);
            var licenseFromFile = await JsonSerializer.DeserializeAsync<OrganizationLicense>(fs);

            AssertHelper.AssertPropertyEqual(license, licenseFromFile, "SignatureBytes");

            // Assertion: should have updated and saved the organization
            // Properties excluded from the comparison below are exceptions to the rule that the Organization mirrors
            // the OrganizationLicense
            await sutProvider.GetDependency<IOrganizationService>()
                .Received(1)
                .ReplaceAndUpdateCacheAsync(Arg.Is<Organization>(
                    org => AssertPropertyEqual(license, org,
                        "Id", "MaxStorageGb", "Issued", "Refresh", "Version", "Trial", "LicenseType",
                        "Hash", "Signature", "SignatureBytes", "InstallationId", "Expires", "ExpirationWithoutGracePeriod",
                        "LimitCollectionCreationDeletion") &&
                         // Same property but different name, use explicit mapping
                         org.ExpirationDate == license.Expires));
        }
        finally
        {
            // Clean up temporary directory
            Directory.Delete(OrganizationLicenseDirectory.Value, true);
        }
    }

    // Wrapper to compare 2 objects that are different types
    private bool AssertPropertyEqual(OrganizationLicense expected, Organization actual, params string[] excludedPropertyStrings)
    {
        AssertHelper.AssertPropertyEqual(expected, actual, excludedPropertyStrings);
        return true;
    }
}
