using System.Text.Json;
using Bit.Core.Services;
using Core.Auth.Enums;
using Xunit;
using static Bit.Api.IntegrationTest.Auth.Helpers.TwoFactorIntegrationTestHelpers;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Assert helpers for the email two-factor <c>TwoFactorController</c> integration tests: emailed codes and Identity
/// token responses.
/// </summary>
internal static class TwoFactorEmailAssertions
{
    private const string InvalidTwoFactorTokenMessage = "Two-step token is invalid. Try again.";

    /// <summary>
    /// Asserts a code was emailed for the given purpose and that it has the six-digit numeric shape clients
    /// accept, then returns it.
    /// </summary>
    public static string AssertCodeEmailed(IMailService mailService, string accountEmail, TwoFactorEmailPurpose purpose)
    {
        var emailed = FindLatestEmailedTwoFactorCode(mailService, accountEmail);
        Assert.NotNull(emailed);
        Assert.Equal(purpose, emailed.Purpose);
        Assert.Matches("^[0-9]{6}$", emailed.Code);
        return emailed.Code;
    }

    public static void AssertLoggedIn(JsonElement tokenResponse)
    {
        Assert.True(tokenResponse.TryGetProperty("access_token", out var accessToken), tokenResponse.ToString());
        Assert.False(string.IsNullOrWhiteSpace(accessToken.GetString()));
    }

    public static void AssertTwoFactorRejected(JsonElement tokenResponse)
    {
        Assert.False(tokenResponse.TryGetProperty("access_token", out _), tokenResponse.ToString());
        Assert.Equal(InvalidTwoFactorTokenMessage,
            tokenResponse.GetProperty("ErrorModel").GetProperty("Message").GetString());
    }
}
