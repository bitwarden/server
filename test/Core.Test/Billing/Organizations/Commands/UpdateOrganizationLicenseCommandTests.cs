using System.Security.Claims;
using Bit.Core.Billing.Organizations.Commands;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

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
    public async Task UpdateLicenseAsync_TamperedExpirationDate_ThrowsBadRequestException(
        SelfHostedOrganizationDetails selfHostedOrg,
        OrganizationLicense license,
        SutProvider<UpdateOrganizationLicenseCommand> sutProvider)
    {
        // Arrange - This test verifies that tampered expiration dates are rejected during upload
        var globalSettings = sutProvider.GetDependency<IGlobalSettings>();
        globalSettings.LicenseDirectory = LicenseDirectory;
        globalSettings.SelfHosted = true;

        // Create a license with a token that has a valid expiration
        var validTokenExpires = DateTime.UtcNow.AddYears(1);
        var tamperedFileExpires = DateTime.UtcNow.AddYears(10); // Tampered - doesn't match token

        license.Enabled = true;
        license.Issued = DateTime.UtcNow.AddDays(-1);
        license.Expires = tamperedFileExpires; // Tampered expiration
        license.Version = OrganizationLicense.CurrentLicenseFileVersion;
        license.InstallationId = globalSettings.Installation.Id;
        license.LicenseType = LicenseType.Organization;
        license.SelfHost = true;

        // Mock the token validation to return a ClaimsPrincipal with different expiration
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("Expires", validTokenExpires.ToString("O")),
            new Claim("Issued", license.Issued.ToString("O")),
            new Claim("InstallationId", license.InstallationId.ToString()),
            new Claim("LicenseKey", license.LicenseKey),
            new Claim("Enabled", "True"),
            new Claim("SelfHost", "True"),
            new Claim("LicenseType", LicenseType.Organization.ToString()),
            new Claim("PlanType", Bit.Core.Billing.Enums.PlanType.EnterpriseAnnually.ToString()),
            new Claim("Name", license.Name ?? "Test"),
            new Claim("Seats", license.Seats?.ToString() ?? "10"),
            new Claim("UseGroups", license.UseGroups.ToString()),
            new Claim("UseDirectory", license.UseDirectory.ToString()),
            new Claim("UseTotp", license.UseTotp.ToString()),
            new Claim("UsersGetPremium", license.UsersGetPremium.ToString()),
            new Claim("UseEvents", license.UseEvents.ToString()),
            new Claim("Use2fa", license.Use2fa.ToString()),
            new Claim("UseApi", license.UseApi.ToString()),
            new Claim("UsePolicies", license.UsePolicies.ToString()),
            new Claim("UseSso", license.UseSso.ToString()),
            new Claim("UseResetPassword", license.UseResetPassword.ToString()),
            new Claim("UseKeyConnector", license.UseKeyConnector.ToString()),
            new Claim("UseScim", license.UseScim.ToString()),
            new Claim("UseCustomPermissions", license.UseCustomPermissions.ToString()),
            new Claim("UseSecretsManager", license.UseSecretsManager.ToString()),
            new Claim("UsePasswordManager", license.UsePasswordManager.ToString()),
            new Claim("SmSeats", license.SmSeats?.ToString() ?? "5"),
            new Claim("SmServiceAccounts", license.SmServiceAccounts?.ToString() ?? "8"),
            new Claim("UseAdminSponsoredFamilies", license.UseAdminSponsoredFamilies.ToString()),
            new Claim("UseOrganizationDomains", license.UseOrganizationDomains.ToString()),
            new Claim("UseAutomaticUserConfirmation", license.UseAutomaticUserConfirmation.ToString())
        }));

        sutProvider.GetDependency<ILicensingService>().VerifyLicense(license).Returns(true);
        sutProvider.GetDependency<ILicensingService>()
            .GetClaimsPrincipalFromLicense(license)
            .Returns(claimsPrincipal);

        // Ensure organization matches license
        selfHostedOrg.LicenseKey = license.LicenseKey;
        selfHostedOrg.Enabled = license.Enabled;
        selfHostedOrg.PlanType = license.PlanType;
        selfHostedOrg.Seats = license.Seats;
        selfHostedOrg.UseGroups = license.UseGroups;
        selfHostedOrg.UseDirectory = license.UseDirectory;
        selfHostedOrg.UseTotp = license.UseTotp;
        selfHostedOrg.UsersGetPremium = license.UsersGetPremium;
        selfHostedOrg.UseEvents = license.UseEvents;
        selfHostedOrg.Use2fa = license.Use2fa;
        selfHostedOrg.UseApi = license.UseApi;
        selfHostedOrg.UsePolicies = license.UsePolicies;
        selfHostedOrg.UseSso = license.UseSso;
        selfHostedOrg.UseResetPassword = license.UseResetPassword;
        selfHostedOrg.UseKeyConnector = license.UseKeyConnector;
        selfHostedOrg.UseScim = license.UseScim;
        selfHostedOrg.UseCustomPermissions = license.UseCustomPermissions;
        selfHostedOrg.UseSecretsManager = license.UseSecretsManager;
        selfHostedOrg.UsePasswordManager = license.UsePasswordManager;
        selfHostedOrg.SmSeats = license.SmSeats;
        selfHostedOrg.SmServiceAccounts = license.SmServiceAccounts;
        selfHostedOrg.UseAdminSponsoredFamilies = license.UseAdminSponsoredFamilies;
        selfHostedOrg.UseOrganizationDomains = license.UseOrganizationDomains;
        selfHostedOrg.UseAutomaticUserConfirmation = license.UseAutomaticUserConfirmation;
        selfHostedOrg.Name = license.Name ?? "Test";

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sutProvider.Sut.UpdateLicenseAsync(selfHostedOrg, license, null));
    }
}
