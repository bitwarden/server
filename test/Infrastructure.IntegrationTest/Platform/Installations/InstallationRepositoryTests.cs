using Bit.Core.Platform.Installations;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Platform.Installations;

public class InstallationRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetByIdAsync_Works(IInstallationRepository installationRepository)
    {
        var installation = await installationRepository.CreateAsync(new Installation
        {
            Email = "test@email.com",
            Key = "installation_key",
            Enabled = true,
        });

        var retrievedInstallation = await installationRepository.GetByIdAsync(installation.Id);

        Assert.NotNull(retrievedInstallation);
        Assert.Equal("installation_key", retrievedInstallation.Key);
    }

    [DatabaseTheory, DatabaseData]
    public async Task UpdateAsync_Works(IInstallationRepository installationRepository)
    {
        var installation = await installationRepository.CreateAsync(new Installation
        {
            Email = "test@email.com",
            Key = "installation_key",
            Enabled = true,
        });

        var now = DateTime.UtcNow;

        installation.LastActivityDate = now;

        await installationRepository.ReplaceAsync(installation);

        var retrievedInstallation = await installationRepository.GetByIdAsync(installation.Id);

        Assert.NotNull(retrievedInstallation.LastActivityDate);
        Assert.Equal(now, retrievedInstallation.LastActivityDate.Value, LaxDateTimeComparer.Default);
    }
}
