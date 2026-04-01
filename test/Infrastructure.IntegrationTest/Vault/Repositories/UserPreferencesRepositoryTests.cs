using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Vault.Repositories;

public class UserPreferencesRepositoryTests
{
    [Theory, DatabaseData]
    public async Task CreateAsync_Success(
        IUserRepository userRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var preferences = UserPreferences.Create(user.Id, "encrypted-data");
        await userPreferencesRepository.CreateAsync(preferences);

        Assert.NotEqual(Guid.Empty, preferences.Id);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdAsync_Exists_ReturnsPreferences(
        IUserRepository userRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var preferences = UserPreferences.Create(user.Id, "encrypted-data");
        await userPreferencesRepository.CreateAsync(preferences);

        var result = await userPreferencesRepository.GetByUserIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(preferences.Id, result.Id);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("encrypted-data", result.Data);
    }

    [Theory, DatabaseData]
    public async Task GetByUserIdAsync_NotExists_ReturnsNull(
        IUserPreferencesRepository userPreferencesRepository)
    {
        var result = await userPreferencesRepository.GetByUserIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_Success(
        IUserRepository userRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var preferences = UserPreferences.Create(user.Id, "original-data");
        await userPreferencesRepository.CreateAsync(preferences);

        preferences.Update("updated-data");
        await userPreferencesRepository.ReplaceAsync(preferences);

        var result = await userPreferencesRepository.GetByUserIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal("updated-data", result.Data);
    }

    [Theory, DatabaseData]
    public async Task DeleteByUserIdAsync_Success(
        IUserRepository userRepository,
        IUserPreferencesRepository userPreferencesRepository)
    {
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = $"test+{Guid.NewGuid()}@email.com",
            ApiKey = "TEST",
            SecurityStamp = "stamp",
        });

        var preferences = UserPreferences.Create(user.Id, "encrypted-data");
        await userPreferencesRepository.CreateAsync(preferences);

        await userPreferencesRepository.DeleteByUserIdAsync(user.Id);

        var result = await userPreferencesRepository.GetByUserIdAsync(user.Id);

        Assert.Null(result);
    }
}
