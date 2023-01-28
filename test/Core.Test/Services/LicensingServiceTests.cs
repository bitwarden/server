using System.Text.Json;
using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class LicensingServiceTests
{
    private static string licenseFilePath(Guid orgId) =>
        Path.Combine(OrganizationLicenseDirectory.Value, $"{orgId}.json");
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
}
