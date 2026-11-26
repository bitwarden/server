using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers that seed users directly in the database.
/// </summary>
public static class UserTestHelpers
{
    public static async Task<User> CreateUserAsync(string email, string name, IdentityApplicationFactory factory, string masterPasswordSalt = null)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();

        var user = new User
        {
            Email = email,
            Id = Guid.NewGuid(),
            Name = name,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test_api_key",
            MasterPasswordSalt = masterPasswordSalt,
        };

        await userRepository.CreateAsync(user);

        return user;
    }

    /// <summary>
    /// Reads the user from the database, applies the change, and saves the user.
    /// </summary>
    public static async Task UpdateUserAsync(IdentityApplicationFactory factory, Guid userId, Action<User> change)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(userId);
        change(user!);
        await userRepository.ReplaceAsync(user!);
    }
}
