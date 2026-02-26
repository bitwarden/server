using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Licenses;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
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
            new(OrganizationLicenseConstants.UseAutomaticUserConfirmation, "true"),
            new(OrganizationLicenseConstants.UseDisableSmAdsForUsers, "true"),
            new(OrganizationLicenseConstants.UsePhishingBlocker, "true"),
            new(OrganizationLicenseConstants.MaxStorageGb, "5"),
            new(OrganizationLicenseConstants.Issued, DateTime.Now.AddDays(-1).ToString("O")),
            new(OrganizationLicenseConstants.Refresh, DateTime.Now.AddMonths(1).ToString("O")),
            new(OrganizationLicenseConstants.ExpirationWithoutGracePeriod, DateTime.Now.AddMonths(12).ToString("O")),
            new(OrganizationLicenseConstants.Trial, "false"),
            new(OrganizationLicenseConstants.LimitCollectionCreationDeletion, "true"),
            new(OrganizationLicenseConstants.AllowAdminAccessToAllCollectionItems, "true"),
            new(OrganizationLicenseConstants.UseMyItems, "true")
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
                    org.UseAutomaticUserConfirmation == true &&
                    org.UseDisableSmAdsForUsers == true &&
                    org.UsePhishingBlocker == true &&
                    org.UseMyItems));
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

    [Theory, BitAutoData]
    public async Task UpdateLicenseAsync_WrongInstallationIdInClaims_ThrowsBadRequestException(
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
        license.LicenseType = LicenseType.Organization;
        license.Token = "test-token"; // Indicates this is a claims-based license
        sutProvider.GetDependency<ILicensingService>().VerifyLicense(license).Returns(true);

        // Create a ClaimsPrincipal with WRONG installation ID
        var wrongInstallationId = Guid.NewGuid(); // Different from globalSettings.Installation.Id
        var claims = new List<Claim>
        {
            new(OrganizationLicenseConstants.LicenseType, ((int)LicenseType.Organization).ToString()),
            new(OrganizationLicenseConstants.InstallationId, wrongInstallationId.ToString()),
            new(OrganizationLicenseConstants.Enabled, "true"),
            new(OrganizationLicenseConstants.SelfHost, "true")
        };
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns(claimsPrincipal);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateLicenseAsync(selfHostedOrg, license, null));

        Assert.Contains("The installation ID does not match the current installation.", exception.Message);

        // Verify organization was NOT saved
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
    }

    [Theory, BitAutoData]
    public async Task UpdateLicenseAsync_ExpiredLicenseWithoutClaims_ThrowsBadRequestException(
        SelfHostedOrganizationDetails selfHostedOrg,
        OrganizationLicense license,
        SutProvider<UpdateOrganizationLicenseCommand> sutProvider)
    {
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        globalSettings.LicenseDirectory = LicenseDirectory;
        globalSettings.SelfHosted = true;

        // Setup legacy license (no Token, no claims)
        license.Token = null; // Legacy license
        license.Enabled = true;
        license.Issued = DateTime.Now.AddDays(-2);
        license.Expires = DateTime.Now.AddDays(-1); // Expired yesterday
        license.Version = OrganizationLicense.CurrentLicenseFileVersion;
        license.InstallationId = globalSettings.Installation.Id;
        license.LicenseType = LicenseType.Organization;
        license.SelfHost = true;

        sutProvider.GetDependency<ILicensingService>().VerifyLicense(license).Returns(true);
        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns((ClaimsPrincipal)null); // No claims for legacy license

        // Passing values for SelfHostedOrganizationDetails.CanUseLicense
        license.Seats = null;
        license.MaxCollections = null;
        license.UseGroups = true;
        license.UsePolicies = true;
        license.UseSso = true;
        license.UseKeyConnector = true;
        license.UseScim = true;
        license.UseCustomPermissions = true;
        license.UseResetPassword = true;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateLicenseAsync(selfHostedOrg, license, null));

        Assert.Contains("The license has expired.", exception.Message);

        // Verify organization was NOT saved
        await sutProvider.GetDependency<IOrganizationService>()
            .DidNotReceive()
            .ReplaceAndUpdateCacheAsync(Arg.Any<Organization>());
    }

    [Fact]
    public async Task UpdateLicenseAsync_ExtractsAllClaimsBasedProperties_WhenClaimsPrincipalProvided()
    {
        // This test ensures that when new properties are added to OrganizationLicense,
        // they are automatically extracted from JWT claims in UpdateOrganizationLicenseCommand.
        // If a new constant is added to OrganizationLicenseConstants but not extracted,
        // this test will fail with a clear message showing which properties are missing.

        // 1. Get all OrganizationLicenseConstants
        var constantFields = typeof(OrganizationLicenseConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => f.GetValue(null) as string)
            .ToList();

        // 2. Define properties that should be excluded (not claims-based or intentionally not extracted)
        var excludedProperties = new HashSet<string>
        {
            "Version",        // Not in claims system (only in deprecated property-based licenses)
            "Hash",           // Signature-related, not extracted from claims
            "Signature",      // Signature-related, not extracted from claims
            "SignatureBytes", // Computed from Signature, not a claim
            "Token",          // The JWT itself, not extracted from claims
            "Id"              // Cloud org ID from license, not used - self-hosted org has its own separate ID
        };

        // 3. Get properties that should be extracted from claims
        var propertiesThatShouldBeExtracted = constantFields
            .Where(c => !excludedProperties.Contains(c))
            .ToHashSet();

        // 4. Read UpdateOrganizationLicenseCommand source code
        var commandSourcePath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "..",
            "src", "Core", "Billing", "Organizations", "Commands", "UpdateOrganizationLicenseCommand.cs");
        var sourceCode = await File.ReadAllTextAsync(commandSourcePath);

        // 5. Find all GetValue calls that extract properties from claims
        // Pattern matches: license.PropertyName = claimsPrincipal.GetValue<Type>(OrganizationLicenseConstants.PropertyName)
        var extractedProperties = new HashSet<string>();
        var getValuePattern = @"claimsPrincipal\.GetValue<[^>]+>\(OrganizationLicenseConstants\.(\w+)\)";
        var matches = Regex.Matches(sourceCode, getValuePattern);

        foreach (Match match in matches)
        {
            extractedProperties.Add(match.Groups[1].Value);
        }

        // 6. Find missing extractions
        var missingExtractions = propertiesThatShouldBeExtracted
            .Except(extractedProperties)
            .OrderBy(p => p)
            .ToList();

        // 7. Build error message with guidance if there are missing extractions
        var errorMessage = "";
        if (missingExtractions.Any())
        {
            errorMessage = $"The following constants in OrganizationLicenseConstants are NOT extracted from claims in UpdateOrganizationLicenseCommand:\n";
            errorMessage += string.Join("\n", missingExtractions.Select(p => $"  - {p}"));
            errorMessage += "\n\nPlease add the following lines to UpdateOrganizationLicenseCommand.cs in the 'if (claimsPrincipal != null)' block:\n";
            foreach (var prop in missingExtractions)
            {
                errorMessage += $"  license.{prop} = claimsPrincipal.GetValue<TYPE>(OrganizationLicenseConstants.{prop});\n";
            }
        }

        // 8. Assert - if this fails, the error message guides the developer to add the extraction
        // Note: We don't check for "extra extractions" because that would be a compile error
        // (can't reference OrganizationLicenseConstants.Foo if Foo doesn't exist)
        Assert.True(
            !missingExtractions.Any(),
            $"\n{errorMessage}");
    }

    // Wrapper to compare 2 objects that are different types
    private bool AssertPropertyEqual(OrganizationLicense expected, Organization actual, params string[] excludedPropertyStrings)
    {
        AssertHelper.AssertPropertyEqual(expected, actual, excludedPropertyStrings);
        return true;
    }
}
