using System.Text;
using System.Text.Json;
using Bit.Core.Auth.Identity;
using Bit.Test.Common.Helpers;
using Xunit;

#nullable enable

namespace Bit.Identity.IntegrationTest.Grants;

/// <summary>
/// Assertions over the claims carried in an issued access token.
/// </summary>
public static class AccessTokenAssertions
{
    /// <summary>
    /// Decodes the payload segment of a JWT into its JSON claims for inspection.
    /// </summary>
    public static JsonElement ReadPayload(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    /// <summary>
    /// Asserts the access token names the device the user logged in on. API endpoints read the requesting device
    /// from these claims.
    /// </summary>
    public static void AssertDeviceClaims(string accessToken, string deviceIdentifier, DeviceType deviceType)
    {
        var payload = ReadPayload(accessToken);
        Assert.Equal(deviceIdentifier,
            AssertHelper.AssertJsonProperty(payload, Claims.Device, JsonValueKind.String).GetString());
        Assert.Equal(deviceType.ToString(),
            AssertHelper.AssertJsonProperty(payload, Claims.DeviceType, JsonValueKind.String).GetString());
    }
}
