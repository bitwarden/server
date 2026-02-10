using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Auth.Repositories;

public class UserRepositoryTests
{
    /// <summary>
    /// Ensures the creation of a new user sets the MasterPasswordSalt to match the Email regardless of the value provided  for the MasterPasswordSalt.
    /// This will need to be changed when PM-30351 is completed and the MasterPasswordSalt is allowed to deviate from the Email.
    /// </summary>
    /// <param name="userRepository"></param>
    /// <returns></returns>
    [Theory, DatabaseData]
    public async Task CreateAsync_ShouldSetMasterPasswordSaltToEmail(
        IUserRepository userRepository)
    {
        // Arrange
        var email = $"test+{Guid.NewGuid()}@example.com";
        var passwordSalt = "NotTrackedSalt";
        var user = new User
        {
            Name = "Test User",
            Email = email,
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            MasterPassword = "password_hash",
            MasterPasswordSalt = passwordSalt
        };

        // Act
        user = await userRepository.CreateAsync(user);

        // Assert
        var createdUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(createdUser);
        Assert.Equal(createdUser.Email, createdUser.MasterPasswordSalt);
        Assert.NotEqual(passwordSalt, createdUser.MasterPasswordSalt);
    }

    /// <summary>
    /// Ensures the update of a user sets the MasterPasswordSalt to match the Email regardless of the value provided for the MasterPasswordSalt.
    /// This will need to be changed when PM-30351 is completed and the MasterPasswordSalt is allowed to deviate from the Email.
    /// </summary>
    /// <param name="userRepository"></param>
    /// <returns></returns>
    [Theory, DatabaseData]
    public async Task ReplaceAsync_ShouldUpdateMasterPasswordSaltToMatchEmail(
        IUserRepository userRepository)
    {
        // Arrange
        var originalEmail = $"original+{Guid.NewGuid()}@example.com";
        var passwordSalt = "NotTrackedSalt";
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = originalEmail,
            ApiKey = "TEST",
            SecurityStamp = "stamp",
            MasterPassword = "password_hash",
            MasterPasswordSalt = passwordSalt
        });

        // Act
        var newEmail = $"updated+{Guid.NewGuid()}@example.com";
        user.Email = newEmail;
        await userRepository.ReplaceAsync(user);

        // Assert
        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.Equal(updatedUser.Email, updatedUser.MasterPasswordSalt);
        Assert.NotEqual(passwordSalt, updatedUser.MasterPasswordSalt);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_ShouldKeepMasterPasswordSaltNullWhenNoMasterPassword(
        IUserRepository userRepository)
    {
        // Arrange
        var originalEmail = $"original+{Guid.NewGuid()}@example.com";
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = originalEmail,
            ApiKey = "TEST",
            SecurityStamp = "stamp"
        });

        // Act
        var newEmail = $"updated+{Guid.NewGuid()}@example.com";
        user.Email = newEmail;
        await userRepository.ReplaceAsync(user);

        // Assert
        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(newEmail, updatedUser.Email);
        Assert.Null(updatedUser.MasterPasswordSalt);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_ShouldSetMasterPasswordSaltToNullWhenNoMasterPassword(
    IUserRepository userRepository)
    {
        // Arrange
        var originalEmail = $"original+{Guid.NewGuid()}@example.com";
        var user = await userRepository.CreateAsync(new User
        {
            Name = "Test User",
            Email = originalEmail,
            ApiKey = "TEST",
            SecurityStamp = "stamp"
        });

        // Act
        await userRepository.ReplaceAsync(user);

        // Assert
        var updatedUser = await userRepository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Null(updatedUser.MasterPasswordSalt);
    }
}
