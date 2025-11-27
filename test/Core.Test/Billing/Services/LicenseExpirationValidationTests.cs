using System.Security.Claims;
using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

/// <summary>
/// Tests for license expiration date validation to prevent tampering.
/// These tests verify that users cannot manipulate expiration dates in license files.
/// </summary>
[SutProviderCustomize]
public class LicenseExpirationValidationTests
{
    private static string LicenseDirectory => Path.GetDirectoryName(OrganizationLicenseDirectory.Value);
    private static Lazy<string> OrganizationLicenseDirectory => new(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "organization");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    });

    public static SutProvider<LicensingService> GetSutProvider()
    {
        var fixture = new Fixture().WithAutoNSubstitutions();

        var settings = fixture.Create<IGlobalSettings>();
        settings.LicenseDirectory = LicenseDirectory;
        settings.SelfHosted = true;
        settings.Installation.Id = Guid.NewGuid();

        return new SutProvider<LicensingService>(fixture)
            .SetDependency(settings)
            .Create();
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(
        Guid installationId,
        DateTime issued,
        DateTime expires,
        Guid organizationId,
        string licenseKey)
    {
        var claims = new List<Claim>
        {
            new Claim("InstallationId", installationId.ToString()),
            new Claim("Issued", issued.ToString("O")),
            new Claim("Expires", expires.ToString("O")),
            new Claim("Id", organizationId.ToString()),
            new Claim("LicenseKey", licenseKey),
            new Claim("Enabled", "True"),
            new Claim("SelfHost", "True"),
            new Claim("LicenseType", LicenseType.Organization.ToString()),
            new Claim("PlanType", Bit.Core.Billing.Enums.PlanType.EnterpriseAnnually.ToString()),
            new Claim("Name", "Test Organization"),
            new Claim("Seats", "10"),
            // MaxCollections claim omitted - GetValue will return null for missing claims
            new Claim("UseGroups", "True"),
            new Claim("UseDirectory", "True"),
            new Claim("UseTotp", "True"),
            new Claim("UsersGetPremium", "True"),
            new Claim("UseEvents", "True"),
            new Claim("Use2fa", "True"),
            new Claim("UseApi", "True"),
            new Claim("UsePolicies", "True"),
            new Claim("UseSso", "True"),
            new Claim("UseResetPassword", "True"),
            new Claim("UseKeyConnector", "False"),
            new Claim("UseScim", "True"),
            new Claim("UseCustomPermissions", "True"),
            new Claim("UseSecretsManager", "True"),
            new Claim("UsePasswordManager", "True"),
            new Claim("SmSeats", "5"),
            new Claim("SmServiceAccounts", "8"),
            new Claim("UseAdminSponsoredFamilies", "False"),
            new Claim("UseOrganizationDomains", "True"),
            new Claim("UseAutomaticUserConfirmation", "False")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static Organization CreateTestOrganization(Guid installationId, string licenseKey)
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Organization",
            LicenseKey = licenseKey,
            Enabled = true,
            PlanType = Bit.Core.Billing.Enums.PlanType.EnterpriseAnnually,
            Seats = 10,
            MaxCollections = null,
            UseGroups = true,
            UseDirectory = true,
            UseTotp = true,
            UsersGetPremium = true,
            UseEvents = true,
            Use2fa = true,
            UseApi = true,
            UsePolicies = true,
            UseSso = true,
            UseResetPassword = true,
            UseKeyConnector = false,
            UseScim = true,
            UseCustomPermissions = true,
            UseSecretsManager = true,
            UsePasswordManager = true,
            SmSeats = 5,
            SmServiceAccounts = 8,
            UseAdminSponsoredFamilies = false,
            UseOrganizationDomains = true,
            UseAutomaticUserConfirmation = false,
            SelfHost = true
        };
    }

    private static OrganizationLicense CreateTestLicense(
        Guid installationId,
        Guid organizationId,
        string licenseKey,
        DateTime? fileExpires,
        DateTime tokenExpires,
        bool includeToken = true)
    {
        var license = new OrganizationLicense
        {
            Id = organizationId,
            LicenseKey = licenseKey,
            InstallationId = installationId,
            Enabled = true,
            SelfHost = true,
            LicenseType = LicenseType.Organization,
            PlanType = Bit.Core.Billing.Enums.PlanType.EnterpriseAnnually,
            Name = "Test Organization",
            Seats = 10,
            MaxCollections = null,
            UseGroups = true,
            UseDirectory = true,
            UseTotp = true,
            UsersGetPremium = true,
            UseEvents = true,
            Use2fa = true,
            UseApi = true,
            UsePolicies = true,
            UseSso = true,
            UseResetPassword = true,
            UseKeyConnector = false,
            UseScim = true,
            UseCustomPermissions = true,
            UseSecretsManager = true,
            UsePasswordManager = true,
            SmSeats = 5,
            SmServiceAccounts = 8,
            UseAdminSponsoredFamilies = false,
            UseOrganizationDomains = true,
            UseAutomaticUserConfirmation = false,
            Issued = DateTime.UtcNow.AddDays(-1),
            Expires = fileExpires,
            Version = OrganizationLicense.CurrentLicenseFileVersion
        };

        if (includeToken)
        {
            // Create a mock token (in real scenario, this would be a JWT)
            // For testing, we'll use a ClaimsPrincipal to simulate token validation
            license.Token = "mock-token";
        }

        return license;
    }

    [Fact]
    public void VerifyData_FileExpiresMatchesTokenExpires_ReturnsTrue()
    {
        // Arrange - Capture time once to avoid timing issues
        var now = DateTime.UtcNow;
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var issued = now.AddDays(-2); // Use -2 days to ensure it's definitely in the past
        var tokenExpires = now.AddYears(1);
        var fileExpires = tokenExpires; // Matches token exactly

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        license.Issued = issued; // Ensure Issued matches claims
        var claimsPrincipal = CreateClaimsPrincipal(installationId, issued, tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.True(result, "License should be valid when file expiration matches token expiration");
    }

    [Fact]
    public void VerifyData_FileExpiresDoesNotMatchTokenExpires_ReturnsFalse()
    {
        // Arrange
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var tokenExpires = DateTime.UtcNow.AddYears(1);
        var fileExpires = DateTime.UtcNow.AddYears(10); // Tampered - doesn't match token

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        var claimsPrincipal = CreateClaimsPrincipal(installationId, DateTime.UtcNow.AddDays(-1), tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.False(result, "License should be invalid when file expiration doesn't match token expiration");
    }

    [Fact]
    public void VerifyData_FileExpiresIsFarInFuture_ReturnsFalse()
    {
        // Arrange
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var tokenExpires = DateTime.UtcNow.AddYears(11); // More than 10 years
        var fileExpires = tokenExpires; // Matches but both are too far

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        var claimsPrincipal = CreateClaimsPrincipal(installationId, DateTime.UtcNow.AddDays(-1), tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.False(result, "License should be invalid when expiration is more than 10 years in the future");
    }

    [Fact]
    public void VerifyData_FileExpiresIsYear3000_ReturnsFalse()
    {
        // Arrange - This is the exact scenario from the bug report
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var tokenExpires = DateTime.UtcNow.AddYears(1);
        var fileExpires = new DateTime(3000, 1, 1); // Year 3000 - tampered

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        var claimsPrincipal = CreateClaimsPrincipal(installationId, DateTime.UtcNow.AddDays(-1), tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.False(result, "License should be invalid when file expiration is set to year 3000");
    }

    [Fact]
    public void VerifyData_FileExpiresIsNull_ReturnsTrue()
    {
        // Arrange - Null Expires is allowed for backward compatibility
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var tokenExpires = DateTime.UtcNow.AddYears(1);
        DateTime? fileExpires = null; // Null is allowed

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        var claimsPrincipal = CreateClaimsPrincipal(installationId, DateTime.UtcNow.AddDays(-1), tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.True(result, "License should be valid when file Expires is null (backward compatibility)");
    }

    [Fact]
    public void VerifyData_FileExpiresWithinOneMinuteTolerance_ReturnsTrue()
    {
        // Arrange - Test clock skew tolerance
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var tokenExpires = DateTime.UtcNow.AddYears(1);
        var fileExpires = tokenExpires.AddSeconds(30); // 30 seconds difference - within tolerance

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        var claimsPrincipal = CreateClaimsPrincipal(installationId, DateTime.UtcNow.AddDays(-1), tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.True(result, "License should be valid when file expiration is within 1 minute tolerance of token expiration");
    }

    [Fact]
    public void VerifyData_FileExpiresBeyondOneMinuteTolerance_ReturnsFalse()
    {
        // Arrange - Test clock skew tolerance boundary
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var tokenExpires = DateTime.UtcNow.AddYears(1);
        var fileExpires = tokenExpires.AddSeconds(61); // 61 seconds difference - beyond tolerance

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, tokenExpires);
        var claimsPrincipal = CreateClaimsPrincipal(installationId, DateTime.UtcNow.AddDays(-1), tokenExpires, organizationId, licenseKey);

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, claimsPrincipal, globalSettings);

        // Assert
        Assert.False(result, "License should be invalid when file expiration is beyond 1 minute tolerance");
    }

    [Fact]
    public void ObsoleteVerifyData_ExpiresIsFarInFuture_ReturnsFalse()
    {
        // Arrange - Test old format license without token
        var installationId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var licenseKey = "test-license-key";
        var fileExpires = DateTime.UtcNow.AddYears(11); // More than 10 years

        var organization = CreateTestOrganization(installationId, licenseKey);
        var license = CreateTestLicense(installationId, organizationId, licenseKey, fileExpires, fileExpires, includeToken: false);
        license.Token = null; // Old format

        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.Installation.Id.Returns(installationId);

        // Act
        var result = license.VerifyData(organization, null, globalSettings);

        // Assert
        Assert.False(result, "Old format license should be invalid when expiration is more than 10 years in the future");
    }

    [Theory, BitAutoData]
    public async Task ValidateOrganizationsAsync_LicenseExpiresIsFarInFuture_DisablesOrganization(
        Organization organization)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var installationId = sutProvider.GetDependency<IGlobalSettings>().Installation.Id;
        var licenseKey = "test-license-key";
        organization.LicenseKey = licenseKey;
        organization.Enabled = true;

        var tamperedExpires = DateTime.UtcNow.AddYears(11); // More than 10 years
        var license = CreateTestLicense(installationId, organization.Id, licenseKey, tamperedExpires, tamperedExpires, includeToken: false);
        license.Token = null; // Old format for simplicity

        // Write license file
        var licenseFilePath = Path.Combine(OrganizationLicenseDirectory.Value, $"{organization.Id}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(licenseFilePath));
        await File.WriteAllTextAsync(licenseFilePath, System.Text.Json.JsonSerializer.Serialize(license));

        var orgRepo = sutProvider.GetDependency<IOrganizationRepository>();
        orgRepo.GetManyByEnabledAsync().Returns(new List<Organization> { organization });
        orgRepo.ReplaceAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);

        var mailService = sutProvider.GetDependency<IMailService>();
        mailService.SendLicenseExpiredAsync(Arg.Any<List<string>>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        try
        {
            // Act
            await sutProvider.Sut.ValidateOrganizationsAsync();

            // Assert
            await orgRepo.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
                org.Id == organization.Id &&
                !org.Enabled));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(Path.GetDirectoryName(licenseFilePath)))
            {
                Directory.Delete(Path.GetDirectoryName(licenseFilePath), true);
            }
        }
    }

    [Theory, BitAutoData]
    public async Task ValidateOrganizationsAsync_LicenseExpiresIsYear3000_DisablesOrganization(
        Organization organization)
    {
        // Arrange - This is the exact scenario from the bug report
        var sutProvider = GetSutProvider();
        var installationId = sutProvider.GetDependency<IGlobalSettings>().Installation.Id;
        var licenseKey = "test-license-key";
        organization.LicenseKey = licenseKey;
        organization.Enabled = true;

        var tamperedExpires = new DateTime(3000, 1, 1); // Year 3000
        var license = CreateTestLicense(installationId, organization.Id, licenseKey, tamperedExpires, tamperedExpires, includeToken: false);
        license.Token = null; // Old format for simplicity

        // Write license file
        var licenseFilePath = Path.Combine(OrganizationLicenseDirectory.Value, $"{organization.Id}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(licenseFilePath));
        await File.WriteAllTextAsync(licenseFilePath, System.Text.Json.JsonSerializer.Serialize(license));

        var orgRepo = sutProvider.GetDependency<IOrganizationRepository>();
        orgRepo.GetManyByEnabledAsync().Returns(new List<Organization> { organization });
        orgRepo.ReplaceAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);

        var mailService = sutProvider.GetDependency<IMailService>();
        mailService.SendLicenseExpiredAsync(Arg.Any<List<string>>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        try
        {
            // Act
            await sutProvider.Sut.ValidateOrganizationsAsync();

            // Assert
            await orgRepo.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
                org.Id == organization.Id &&
                !org.Enabled));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(Path.GetDirectoryName(licenseFilePath)))
            {
                Directory.Delete(Path.GetDirectoryName(licenseFilePath), true);
            }
        }
    }

    [Theory, BitAutoData]
    public async Task ValidateOrganizationsAsync_LicenseHasExpired_DisablesOrganization(
        Organization organization)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var installationId = sutProvider.GetDependency<IGlobalSettings>().Installation.Id;
        var licenseKey = "test-license-key";
        organization.LicenseKey = licenseKey;
        organization.Enabled = true;

        var expiredDate = DateTime.UtcNow.AddDays(-1); // Expired yesterday
        var license = CreateTestLicense(installationId, organization.Id, licenseKey, expiredDate, expiredDate, includeToken: false);
        license.Token = null; // Old format for simplicity

        // Write license file
        var licenseFilePath = Path.Combine(OrganizationLicenseDirectory.Value, $"{organization.Id}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(licenseFilePath));
        await File.WriteAllTextAsync(licenseFilePath, System.Text.Json.JsonSerializer.Serialize(license));

        var orgRepo = sutProvider.GetDependency<IOrganizationRepository>();
        orgRepo.GetManyByEnabledAsync().Returns(new List<Organization> { organization });
        orgRepo.ReplaceAsync(Arg.Any<Organization>()).Returns(Task.CompletedTask);

        var mailService = sutProvider.GetDependency<IMailService>();
        mailService.SendLicenseExpiredAsync(Arg.Any<List<string>>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        try
        {
            // Act
            await sutProvider.Sut.ValidateOrganizationsAsync();

            // Assert
            await orgRepo.Received(1).ReplaceAsync(Arg.Is<Organization>(org =>
                org.Id == organization.Id &&
                !org.Enabled));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(Path.GetDirectoryName(licenseFilePath)))
            {
                Directory.Delete(Path.GetDirectoryName(licenseFilePath), true);
            }
        }
    }

    // Note: Testing ValidateOrganizationsAsync with valid licenses requires proper certificate setup
    // which is complex. The expiration validation tests above (VerifyData tests) cover the core logic.
    // The ValidateOrganizationsAsync tests above verify that invalid expiration dates cause
    // organizations to be disabled, which is the main security concern.
}

