using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.IntegrationTestCommon.Factories;

namespace Bit.Identity.IntegrationTest.Helpers;

/// <summary>
/// Arrange helpers for tests of the password grant.
/// </summary>
public static class PasswordGrantTestHelpers
{
    public const string DefaultPassword = "master_password_hash";
    public const string DefaultUsername = "test@email.qa";
    public const string DefaultDeviceIdentifier = "test_identifier";
    public const DeviceType DefaultDeviceType = DeviceType.FirefoxBrowser;
    public const string DefaultDeviceName = "firefox";

    public static async Task RegisterUserAsync(
        IdentityApplicationFactory factory,
        string username = DefaultUsername,
        string password = DefaultPassword
)
    {
        await factory.RegisterNewIdentityFactoryUserAsync(
            new RegisterFinishRequestModel
            {
                Email = username,
                MasterPasswordHash = password,
                Kdf = KdfType.PBKDF2_SHA256,
                KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
                UserAsymmetricKeys = new KeysRequestModel
                {
                    PublicKey = Bit.Test.Common.Constants.TestEncryptionConstants.PublicKey,
                    EncryptedPrivateKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring
                },
                UserSymmetricKey = Bit.Test.Common.Constants.TestEncryptionConstants.AES256_CBC_HMAC_Encstring,
            });
    }
}
