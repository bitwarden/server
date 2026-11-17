using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Auth.Models;
using Bit.IntegrationTestCommon.Factories;
using Microsoft.EntityFrameworkCore;

#nullable enable

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange and act helpers for tests of the <c>refresh_token</c> grant.
/// </summary>
public static class RefreshTokenGrantTestHelpers
{
    /// <summary>
    /// Registers the default password-grant user and logs in with the password grant on the given client.
    /// </summary>
    public static async Task<(User User, string AccessToken, string RefreshToken)> RegisterAndLoginAsync(
        IdentityApplicationFactory factory, string clientId = "web")
    {
        await PasswordGrantTestHelpers.RegisterUserAsync(factory);
        var (accessToken, refreshToken) = await factory.TokenFromPasswordAsync(
            PasswordGrantTestHelpers.DefaultUsername, PasswordGrantTestHelpers.DefaultPassword, clientId: clientId);
        var user = await factory.Services.GetRequiredService<IUserRepository>()
            .GetByEmailAsync(PasswordGrantTestHelpers.DefaultUsername);
        return (user!, accessToken, refreshToken);
    }

    public static async Task<HttpContext> PostRefreshTokenAsync(
        IdentityApplicationFactory factory, string refreshToken, string clientId = "web")
    {
        return await factory.Server.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", clientId },
            { "refresh_token", refreshToken },
        }));
    }

    /// <summary>
    /// Reads the stored refresh token grant of a user who logged in once.
    /// </summary>
    public static async Task<Grant> GetRefreshTokenGrantAsync(IdentityApplicationFactory factory, Guid userId)
    {
        var databaseContext = factory.GetDatabaseContext();
        return await databaseContext.Grants.AsNoTracking()
            .SingleAsync(g => g.SubjectId == userId.ToString() && g.Type == "refresh_token");
    }

    /// <summary>
    /// Changes only the stored expiration column. The server checks the lifetime in the grant data, so this does not
    /// expire the token.
    /// </summary>
    public static async Task SetRefreshTokenGrantExpirationAsync(
        IdentityApplicationFactory factory, Guid userId, DateTime expirationDate)
    {
        var databaseContext = factory.GetDatabaseContext();
        var grant = await databaseContext.Grants
            .SingleAsync(g => g.SubjectId == userId.ToString() && g.Type == "refresh_token");
        grant.ExpirationDate = expirationDate;
        await databaseContext.SaveChangesAsync();
    }

    public static async Task UpdateUserAsync(IdentityApplicationFactory factory, Guid userId, Action<User> change)
    {
        var userRepository = factory.Services.GetRequiredService<IUserRepository>();
        var user = await userRepository.GetByIdAsync(userId);
        change(user!);
        await userRepository.ReplaceAsync(user!);
    }
}
