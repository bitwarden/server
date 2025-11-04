using System.Security.Claims;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Bit.Core.Test.Billing.Organizations.Commands;

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
        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns((ClaimsPrincipal)null);

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
                        "Hash", "Signature", "SignatureBytes", "InstallationId", "Expires",
                        "ExpirationWithoutGracePeriod", "Token", "LimitCollectionCreationDeletion",
                        "LimitCollectionCreation", "LimitCollectionDeletion", "AllowAdminAccessToAllCollectionItems") &&
                         // Same property but different name, use explicit mapping
                         org.ExpirationDate == license.Expires));
        }
        finally
        {
            // Clean up temporary directory
            Directory.Delete(OrganizationLicenseDirectory.Value, true);
        }
    }

    [Theory, BitAutoData]
    public async Task UpdateLicenseAsync_WithClaimsPrincipal_ExtractsAllPropertiesFromClaims(
        SelfHostedOrganizationDetails selfHostedOrg,
        OrganizationLicense license,
        SutProvider<UpdateOrganizationLicenseCommand> sutProvider)
    {
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        globalSettings.LicenseDirectory = LicenseDirectory;
        globalSettings.SelfHosted = true;

        // Setup license for CanUse validation
        license.Enabled = true;
        license.Issued = DateTime.Now.AddDays(-1);
        license.Expires = DateTime.Now.AddDays(1);
        license.Version = OrganizationLicense.CurrentLicenseFileVersion;
        license.InstallationId = globalSettings.Installation.Id;
        license.LicenseType = LicenseType.Organization;
        license.Token = "test-token"; // Indicates this is a claims-based license
        sutProvider.GetDependency<ILicensingService>().VerifyLicense(license).Returns(true);

        // Create a ClaimsPrincipal with all organization license claims
        var claims = new List<Claim>
        {
            new(OrganizationLicenseConstants.LicenseType, ((int)LicenseType.Organization).ToString()),
            new(OrganizationLicenseConstants.InstallationId, globalSettings.Installation.Id.ToString()),
            new(OrganizationLicenseConstants.Name, "Test Organization"),
            new(OrganizationLicenseConstants.BillingEmail, "billing@test.com"),
            new(OrganizationLicenseConstants.BusinessName, "Test Business"),
            new(OrganizationLicenseConstants.PlanType, ((int)PlanType.EnterpriseAnnually).ToString()),
            new(OrganizationLicenseConstants.Seats, "100"),
            new(OrganizationLicenseConstants.MaxCollections, "50"),
            new(OrganizationLicenseConstants.UsePolicies, "true"),
            new(OrganizationLicenseConstants.UseSso, "true"),
            new(OrganizationLicenseConstants.UseKeyConnector, "true"),
            new(OrganizationLicenseConstants.UseScim, "true"),
            new(OrganizationLicenseConstants.UseGroups, "true"),
            new(OrganizationLicenseConstants.UseDirectory, "true"),
            new(OrganizationLicenseConstants.UseEvents, "true"),
            new(OrganizationLicenseConstants.UseTotp, "true"),
            new(OrganizationLicenseConstants.Use2fa, "true"),
            new(OrganizationLicenseConstants.UseApi, "true"),
            new(OrganizationLicenseConstants.UseResetPassword, "true"),
            new(OrganizationLicenseConstants.Plan, "Enterprise"),
            new(OrganizationLicenseConstants.SelfHost, "true"),
            new(OrganizationLicenseConstants.UsersGetPremium, "true"),
            new(OrganizationLicenseConstants.UseCustomPermissions, "true"),
            new(OrganizationLicenseConstants.Enabled, "true"),
            new(OrganizationLicenseConstants.Expires, DateTime.Now.AddDays(1).ToString("O")),
            new(OrganizationLicenseConstants.LicenseKey, "test-license-key"),
            new(OrganizationLicenseConstants.UsePasswordManager, "true"),
            new(OrganizationLicenseConstants.UseSecretsManager, "true"),
            new(OrganizationLicenseConstants.SmSeats, "25"),
            new(OrganizationLicenseConstants.SmServiceAccounts, "10"),
            new(OrganizationLicenseConstants.UseRiskInsights, "true"),
            new(OrganizationLicenseConstants.UseOrganizationDomains, "true"),
            new(OrganizationLicenseConstants.UseAdminSponsoredFamilies, "true"),
            new(OrganizationLicenseConstants.UseAutomaticUserConfirmation, "true")
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns(claimsPrincipal);

        // Setup selfHostedOrg for CanUseLicense validation
        selfHostedOrg.OccupiedSeatCount = 50; // Less than the 100 seats in the license
        selfHostedOrg.CollectionCount = 10; // Less than the 50 max collections in the license
        selfHostedOrg.GroupCount = 1;
        selfHostedOrg.UseGroups = true;
        selfHostedOrg.UsePolicies = true;
        selfHostedOrg.UseSso = true;
        selfHostedOrg.UseKeyConnector = true;
        selfHostedOrg.UseScim = true;
        selfHostedOrg.UseCustomPermissions = true;
        selfHostedOrg.UseResetPassword = true;

        try
        {
            await sutProvider.Sut.UpdateLicenseAsync(selfHostedOrg, license, null);

            // Assertion: license file should be written to disk
            var filePath = Path.Combine(LicenseDirectory, "organization", $"{selfHostedOrg.Id}.json");
            await using var fs = File.OpenRead(filePath);
            var licenseFromFile = await JsonSerializer.DeserializeAsync<OrganizationLicense>(fs);

            AssertHelper.AssertPropertyEqual(license, licenseFromFile, "SignatureBytes");

            // Assertion: organization should be updated with ALL properties extracted from claims
            await sutProvider.GetDependency<IOrganizationService>()
                .Received(1)
                .ReplaceAndUpdateCacheAsync(Arg.Is<Organization>(org =>
                    org.Name == "Test Organization" &&
                    org.BillingEmail == "billing@test.com" &&
                    org.BusinessName == "Test Business" &&
                    org.PlanType == PlanType.EnterpriseAnnually &&
                    org.Seats == 100 &&
                    org.MaxCollections == 50 &&
                    org.UsePolicies == true &&
                    org.UseSso == true &&
                    org.UseKeyConnector == true &&
                    org.UseScim == true &&
                    org.UseGroups == true &&
                    org.UseDirectory == true &&
                    org.UseEvents == true &&
                    org.UseTotp == true &&
                    org.Use2fa == true &&
                    org.UseApi == true &&
                    org.UseResetPassword == true &&
                    org.Plan == "Enterprise" &&
                    org.SelfHost == true &&
                    org.UsersGetPremium == true &&
                    org.UseCustomPermissions == true &&
                    org.Enabled == true &&
                    org.LicenseKey == "test-license-key" &&
                    org.UsePasswordManager == true &&
                    org.UseSecretsManager == true &&
                    org.SmSeats == 25 &&
                    org.SmServiceAccounts == 10 &&
                    org.UseRiskInsights == true &&
                    org.UseOrganizationDomains == true &&
                    org.UseAdminSponsoredFamilies == true &&
                    org.UseAutomaticUserConfirmation == true));
        }
        finally
        {
            // Clean up temporary directory
            if (Directory.Exists(OrganizationLicenseDirectory.Value))
            {
                Directory.Delete(OrganizationLicenseDirectory.Value, true);
            }
        }
    }

    // Wrapper to compare 2 objects that are different types
    private bool AssertPropertyEqual(OrganizationLicense expected, Organization actual, params string[] excludedPropertyStrings)
    {
        AssertHelper.AssertPropertyEqual(expected, actual, excludedPropertyStrings);
        return true;
    }
}
