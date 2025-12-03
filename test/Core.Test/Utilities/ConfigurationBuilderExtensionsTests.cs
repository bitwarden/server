using Bit.Core.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.KeyPerFile;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class ConfigurationBuilderExtensionsTests
{
    [Fact]
    public void AddKeyPerFile_ShouldRegister_WhenFileConfigDirectoryIsProperlyRegistered()
    {
        // Arrange
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string> { { "globalSettings:installation:fileConfigDirectory", Path.GetTempPath() } });

        // Act
        configurationBuilder.AddKeyPerFile(configurationBuilder.Build());

        // Assert
        var keyPerFileConfigurationSource =
            configurationBuilder.Sources.SingleOrDefault(source => source.GetType().Name == "KeyPerFileConfigurationSource") as KeyPerFileConfigurationSource;
        Assert.NotNull(keyPerFileConfigurationSource);
        Assert.False(keyPerFileConfigurationSource.Optional);
        Assert.True(keyPerFileConfigurationSource.ReloadOnChange);
        Assert.NotNull(keyPerFileConfigurationSource.FileProvider);
    }

    [Fact]
    public void AddKeyPerFile_ShouldNotRegister_WhenFileConfigDirectoryDoesNotExist()
    {
        // Arrange
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string> { { "globalSettings:installation:fileConfigDirectory", "/does/not/exist" } });

        // Act
        configurationBuilder.AddKeyPerFile(configurationBuilder.Build());

        // Assert
        Assert.DoesNotContain(configurationBuilder.Sources, source => source.GetType().Name == "KeyPerFileConfigurationSource");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void AddKeyPerFile_ShouldNotRegister_WhenFileConfigDirectoryIsEmptyString(string fileConfigDirectory)
    {
        // Arrange
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string> { { "globalSettings:installation:fileConfigDirectory", fileConfigDirectory } });

        // Act
        configurationBuilder.AddKeyPerFile(configurationBuilder.Build());

        // Assert
        Assert.DoesNotContain(configurationBuilder.Sources, source => source.GetType().Name == "KeyPerFileConfigurationSource");
    }

    [Fact]
    public void AddKeyPerFile_ShouldNotRegister_WhenFileConfigDirectoryIsNotConfigured()
    {
        // Arrange
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new Dictionary<string, string> { { "globalSettings:installation:SomeArbitraryKey", "/does/not/exist" } });

        // Act
        configurationBuilder.AddKeyPerFile(configurationBuilder.Build());

        // Assert
        Assert.DoesNotContain(configurationBuilder.Sources, source => source.GetType().Name == "KeyPerFileConfigurationSource");
    }
}
