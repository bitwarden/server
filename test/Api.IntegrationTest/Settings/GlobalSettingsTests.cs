using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Api.IntegrationTest.Settings;

public class GlobalSettingsTests : IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;

    public GlobalSettingsTests(ApiApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GlobalSettings_ShouldBeConfigurableViaFiles_WhenFileConfigDirectoryIsSpecified()
    {
        // Arrange
        const string installationKey = "test-installation-key";
        var installationId = Guid.NewGuid().ToString();
        var fileConfigDirectory = Directory.CreateTempSubdirectory();
        try
        {
            _factory.UpdateConfiguration(builder =>
            {
                builder.AddInMemoryCollection(new Dictionary<string, string?> { { "globalSettings:installation:fileConfigDirectory", fileConfigDirectory.FullName } });
                builder.AddKeyPerFile(builder.Build());
            });
            await CreateSecretsFileAsync(fileConfigDirectory, "GlobalSettings__Installation__Key", installationKey);
            await CreateSecretsFileAsync(fileConfigDirectory, "GlobalSettings__Installation__Id", installationId);

            // Act
            var globalSettings = _factory.GetService<IGlobalSettings>();

            // Assert
            Assert.Equal(fileConfigDirectory.FullName, globalSettings.Installation.FileConfigDirectory);
            Assert.Equal(installationKey, globalSettings.Installation.Key);
            Assert.Equal(installationId, globalSettings.Installation.Id.ToString());
        }
        finally
        {
            fileConfigDirectory.Delete(true);
        }
    }

    private static async Task CreateSecretsFileAsync(DirectoryInfo secretsDirectory, string fileName, string secret)
    {
        var secretConfigFile = Path.Combine(secretsDirectory.FullName, fileName);
        await File.WriteAllTextAsync(secretConfigFile, secret);
    }
}
