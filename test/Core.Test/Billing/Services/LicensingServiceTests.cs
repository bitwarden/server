using System.Text.Json;
using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bit.Core.Test.Billing.Services;

[SutProviderCustomize]
public class LicensingServiceTests
{
    private static string licenseFilePath(Guid orgId) =>
        Path.Combine(OrganizationLicenseDirectory.Value, $"{orgId}.json");
    private static string userLicenseFilePath(Guid userId) =>
        Path.Combine(UserLicenseDirectory.Value, $"{userId}.json");
    private static string LicenseDirectory => Path.GetDirectoryName(OrganizationLicenseDirectory.Value);
    private static Lazy<string> OrganizationLicenseDirectory => new(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "organization");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    });
    private static Lazy<string> UserLicenseDirectory => new(() =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "user");
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    });

    public static SutProvider<LicensingService> GetSutProvider(
        string environmentName = "Development")
    {
        var fixture = new Fixture().WithAutoNSubstitutions();

        var settings = fixture.Create<IGlobalSettings>();
        settings.LicenseDirectory = LicenseDirectory;
        settings.SelfHosted = true;

        var environment = fixture.Create<IWebHostEnvironment>();
        environment.EnvironmentName = environmentName;

        return new SutProvider<LicensingService>(fixture)
            .SetDependency(settings)
            .SetDependency(environment)
            .Create();
    }

    private static OrganizationLicense LoadLicense(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Billing", "Services", filename);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<OrganizationLicense>(json);
    }

    [Theory, BitAutoData, OrganizationLicenseCustomize]
    public async Task ReadOrganizationLicense(Organization organization, OrganizationLicense license)
    {
        var sutProvider = GetSutProvider();

        File.WriteAllText(licenseFilePath(organization.Id), JsonSerializer.Serialize(license));

        var actual = await sutProvider.Sut.ReadOrganizationLicenseAsync(organization);
        try
        {
            Assert.Equal(JsonSerializer.Serialize(license), JsonSerializer.Serialize(actual));
        }
        finally
        {
            Directory.Delete(OrganizationLicenseDirectory.Value, true);
        }
    }

    [Theory, BitAutoData]
    public async Task WriteUserLicense_CreatesFileWithCorrectContent(User user, UserLicense license)
    {
        // Arrange
        var sutProvider = GetSutProvider();
        var expectedFilePath = userLicenseFilePath(user.Id);

        try
        {
            // Act
            await sutProvider.Sut.WriteUserLicenseAsync(user, license);

            // Assert
            Assert.True(File.Exists(expectedFilePath));
            var fileContent = await File.ReadAllTextAsync(expectedFilePath);
            var actualLicense = JsonSerializer.Deserialize<UserLicense>(fileContent);

            Assert.Equal(license.LicenseKey, actualLicense.LicenseKey);
            Assert.Equal(license.Id, actualLicense.Id);
            Assert.Equal(license.Expires, actualLicense.Expires);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(UserLicenseDirectory.Value))
            {
                Directory.Delete(UserLicenseDirectory.Value, true);
            }
        }
    }

    [Theory, BitAutoData]
    public async Task WriteUserLicense_CreatesDirectoryIfNotExists(User user, UserLicense license)
    {
        // Arrange
        var sutProvider = GetSutProvider();

        // Ensure directory doesn't exist
        if (Directory.Exists(UserLicenseDirectory.Value))
        {
            Directory.Delete(UserLicenseDirectory.Value, true);
        }

        try
        {
            // Act
            await sutProvider.Sut.WriteUserLicenseAsync(user, license);

            // Assert
            Assert.True(Directory.Exists(UserLicenseDirectory.Value));
            Assert.True(File.Exists(userLicenseFilePath(user.Id)));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(UserLicenseDirectory.Value))
            {
                Directory.Delete(UserLicenseDirectory.Value, true);
            }
        }
    }

    public static TheoryData<string, string, bool> GetClaimsPrincipalFromLicense_TestCases =>
        new()
        {
            // Dev license validates on non-production server (dev cert is in verification list)
            { "test-org-license-dev.json", Environments.Development, true },
            // Dev license fails on production server (dev cert not loaded)
            { "test-org-license-dev.json", Environments.Production, false },
            // Both prod and dev cert is in verification list on dev server
            { "test-org-license-prod.json", Environments.Development, true },
            // Prod license validates on production server
            { "test-org-license-prod.json", Environments.Production, true },
            // Dev license on QA server
            { "test-org-license-dev.json", "QA", true },
            // Prod license on QA server
            { "test-org-license-prod.json", "QA", true },
        };

    [Theory]
    [MemberData(nameof(GetClaimsPrincipalFromLicense_TestCases))]
    public void GetClaimsPrincipalFromLicense_ValidatesCorrectCertPerEnvironment(
        string licenseFile, string environment, bool shouldSucceed)
    {
        var sutProvider = GetSutProvider(environment);
        var license = LoadLicense(licenseFile);

        if (shouldSucceed)
        {
            var result = sutProvider.Sut.GetClaimsPrincipalFromLicense(license);
            Assert.NotNull(result);
        }
        else
        {
            Assert.Throws<BadRequestException>(() =>
                sutProvider.Sut.GetClaimsPrincipalFromLicense(license));
        }
    }
}
