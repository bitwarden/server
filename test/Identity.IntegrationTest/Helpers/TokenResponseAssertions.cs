using System.Text.Json;
using Bit.Core.Enums;
using Bit.Test.Common.Helpers;
using Xunit;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Assertions on the body of a token endpoint response.
/// </summary>
public static class TokenResponseAssertions
{
    private const int SecondsInMinute = 60;
    private const int MinutesInHour = 60;
    public const int SecondsInHour = SecondsInMinute * MinutesInHour;

    public static async Task<JsonDocument> AssertDefaultTokenBodyAsync(HttpContext httpContext, string expectedScope = "api offline_access", int expectedExpiresIn = SecondsInHour * 1)
    {
        var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(httpContext);
        var root = body.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        AssertAccessTokenExists(root);
        AssertExpiresIn(root, expectedExpiresIn);
        AssertTokenType(root);
        AssertScope(root, expectedScope);
        return body;
    }

    private static void AssertTokenType(JsonElement tokenResponse)
    {
        var tokenTypeProperty = AssertHelper.AssertJsonProperty(tokenResponse, "token_type", JsonValueKind.String).GetString();
        Assert.Equal("Bearer", tokenTypeProperty);
    }

    private static int AssertExpiresIn(JsonElement tokenResponse, int expectedExpiresIn = 3600)
    {
        var expiresIn = AssertHelper.AssertJsonProperty(tokenResponse, "expires_in", JsonValueKind.Number).GetInt32();
        Assert.Equal(expectedExpiresIn, expiresIn);
        return expiresIn;
    }

    public static string AssertAccessTokenExists(JsonElement tokenResponse)
    {
        return AssertHelper.AssertJsonProperty(tokenResponse, "access_token", JsonValueKind.String).GetString();
    }

    public static string AssertRefreshTokenExists(JsonElement tokenResponse)
    {
        return AssertHelper.AssertJsonProperty(tokenResponse, "refresh_token", JsonValueKind.String).GetString();
    }

    /// <summary>
    /// Asserts a 400 response whose body holds only the given OAuth error code.
    /// </summary>
    public static async Task AssertErrorAsync(HttpContext httpContext, string expectedError)
    {
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(httpContext);
        var root = body.RootElement;
        Assert.Equal(new[] { "error" }, root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(expectedError, AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString());
    }

    public static Task AssertInvalidGrantAsync(HttpContext httpContext) =>
        AssertErrorAsync(httpContext, "invalid_grant");

    /// <summary>
    /// Asserts a 400 response with the given OAuth error code, <c>invalid_grant</c> by default, whose
    /// <c>ErrorModel</c> holds the given message for the client to show.
    /// </summary>
    public static async Task AssertErrorModelAsync(HttpContext httpContext, string expectedMessage,
        string expectedError = "invalid_grant")
    {
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        using var body = await AssertHelper.AssertResponseTypeIs<JsonDocument>(httpContext);
        var root = body.RootElement;
        Assert.Equal(new[] { "ErrorModel", "error", "error_description" },
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(expectedError, AssertHelper.AssertJsonProperty(root, "error", JsonValueKind.String).GetString());
        var errorModel = AssertHelper.AssertJsonProperty(root, "ErrorModel", JsonValueKind.Object);
        Assert.Equal(expectedMessage,
            AssertHelper.AssertJsonProperty(errorModel, "Message", JsonValueKind.String).GetString());
    }

    /// <summary>
    /// Asserts that the user decryption options say the user has a master password, and that they carry the master
    /// password unlock data. Other decryption options can also be present; use
    /// <see cref="AssertDecryptionOptionFields"/> to check the full set.
    /// </summary>
    public static void AssertMasterPasswordDecryptionOptions(JsonElement tokenResponse, KdfType expectedKdfType,
        int expectedKdfIterations, string expectedMasterKeyEncryptedUserKey, string expectedSalt)
    {
        var userDecryptionOptions =
            AssertHelper.AssertJsonProperty(tokenResponse, "UserDecryptionOptions", JsonValueKind.Object);

        AssertHelper.AssertJsonProperty(userDecryptionOptions, "HasMasterPassword", JsonValueKind.True);
        var objectString = AssertHelper.AssertJsonProperty(userDecryptionOptions, "Object", JsonValueKind.String).ToString();
        Assert.Equal("userDecryptionOptions", objectString);
        var masterPasswordUnlock = AssertHelper.AssertJsonProperty(userDecryptionOptions, "MasterPasswordUnlock", JsonValueKind.Object);
        // MasterPasswordUnlock.Kdf
        var kdf = AssertHelper.AssertJsonProperty(masterPasswordUnlock, "Kdf", JsonValueKind.Object);
        var kdfType = AssertHelper.AssertJsonProperty(kdf, "KdfType", JsonValueKind.Number).GetInt32();
        Assert.Equal((int)expectedKdfType, kdfType);
        var kdfIterations = AssertHelper.AssertJsonProperty(kdf, "Iterations", JsonValueKind.Number).GetInt32();
        Assert.Equal(expectedKdfIterations, kdfIterations);
        // MasterPasswordUnlock.MasterKeyEncryptedUserKey
        var masterKeyEncryptedUserKey = AssertHelper.AssertJsonProperty(masterPasswordUnlock, "MasterKeyEncryptedUserKey", JsonValueKind.String).ToString();
        Assert.Equal(expectedMasterKeyEncryptedUserKey, masterKeyEncryptedUserKey);
        // MasterPasswordUnlock.Salt
        var salt = AssertHelper.AssertJsonProperty(masterPasswordUnlock, "Salt", JsonValueKind.String).ToString();
        Assert.Equal(expectedSalt, salt);
    }

    /// <summary>
    /// Asserts that the user decryption options hold exactly the given fields, so no other decryption option is
    /// present.
    /// </summary>
    public static void AssertDecryptionOptionFields(JsonElement tokenResponse, params string[] expectedFields)
    {
        var userDecryptionOptions =
            AssertHelper.AssertJsonProperty(tokenResponse, "UserDecryptionOptions", JsonValueKind.Object);
        Assert.Equal(expectedFields.Order(StringComparer.Ordinal),
            userDecryptionOptions.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    private static string AssertScopeExists(JsonElement tokenResponse)
    {
        return AssertHelper.AssertJsonProperty(tokenResponse, "scope", JsonValueKind.String).GetString();
    }

    private static void AssertScope(JsonElement tokenResponse, string expectedScope)
    {
        var actualScope = AssertScopeExists(tokenResponse);
        Assert.Equal(expectedScope, actualScope);
    }
}
