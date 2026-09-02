using System.Text.Json;
using AutoFixture;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Test.Billing.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
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

    public static SutProvider<LicensingService> GetSutProvider()
    {
        var fixture = new Fixture().WithAutoNSubstitutions();

        var settings = fixture.Create<IGlobalSettings>();
        settings.LicenseDirectory = LicenseDirectory;
        settings.SelfHosted = true;

        return new SutProvider<LicensingService>(fixture)
            .SetDependency(settings)
            .Create();
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
}
