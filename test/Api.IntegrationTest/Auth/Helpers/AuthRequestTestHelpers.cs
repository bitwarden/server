using Bit.Api.IntegrationTest.Factories;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Arrange helpers for auth request integration tests.
/// </summary>
internal static class AuthRequestTestHelpers
{
    /// <summary>
    /// Stores an unanswered auth request for the user. <paramref name="customize"/> can change any field before the
    /// row is stored.
    /// </summary>
    public static async Task<AuthRequest> SeedAuthRequestAsync(
        IAuthRequestRepository authRequestRepository, Guid userId, Action<AuthRequest>? customize = null)
    {
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            RequestDeviceIdentifier = "requesting-device-identifier",
            RequestDeviceType = DeviceType.Android,
            RequestIpAddress = "1.1.1.1",
            AccessCode = "test_access_code",
            PublicKey = "test_public_key",
            CreationDate = DateTime.UtcNow,
        };
        customize?.Invoke(authRequest);
        return await authRequestRepository.CreateAsync(authRequest);
    }

    /// <summary>
    /// Registers a new account with a unique email and returns its user.
    /// </summary>
    public static async Task<User> RegisterAnotherUserAsync(ApiApplicationFactory factory)
    {
        var email = $"auth-request-controller-test-other-{Guid.NewGuid()}@bitwarden.com";
        await factory.LoginWithNewAccount(email);
        var user = await factory.GetService<IUserRepository>().GetByEmailAsync(email);
        Assert.NotNull(user);
        return user;
    }
}
