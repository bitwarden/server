using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class InstallationRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(IInstallationRepository installationRepository)
    {
        var createdInstallation = await installationRepository.CreateAsync(new Installation
        {
            Email = "test@example.com",
            Key = "test_key",
            Enabled = true,
        });

        // Assert that we get the Id back right away so that we know we set the Id client side
        Assert.NotEqual(createdInstallation.Id, Guid.Empty);

        // Assert that we can find one from the database
        var installation = await installationRepository.GetByIdAsync(createdInstallation.Id);
        Assert.NotNull(installation);

        // Assert the found item has all the data we expect
        Assert.Equal("test@example.com", installation.Email);
        Assert.Equal("test_key", installation.Key);
        Assert.True(installation.Enabled);

        // Assert the items returned from CreateAsync match what is found by id
        Assert.Equal(createdInstallation.Id, installation.Id);
        Assert.Equal(createdInstallation.Email, installation.Email);
        Assert.Equal(createdInstallation.Key, installation.Key);
        Assert.Equal(createdInstallation.Enabled, installation.Enabled);
        // TODO: Assert dates
    }
}
