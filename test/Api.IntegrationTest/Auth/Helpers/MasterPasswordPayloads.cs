using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Api.Request;

namespace Bit.Api.IntegrationTest.Auth.Helpers;

/// <summary>
/// Request-body builders and constant values for master password authentication and unlock payloads.
/// </summary>
internal static class MasterPasswordPayloads
{
    public const string MasterKeyWrappedUserKey =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    /// <summary>
    /// The master password hash that <c>ApiApplicationFactory.LoginWithNewAccount</c> registers by default.
    /// </summary>
    public const string MasterPasswordHash = "master_password_hash";

    public const string NewMasterPasswordHash = "new_master_password_hash";

    public static readonly KdfRequestModel DefaultKdfRequest =
        new() { KdfType = KdfType.PBKDF2_SHA256, Iterations = 600_000 };

    // Builders for the dual-payload auth + unlock blocks. Tests vary KDF and
    // salt; everything else is constant.
    public static MasterPasswordAuthenticationDataRequestModel BuildAuthData(KdfRequestModel kdf, string salt) =>
        new() { Kdf = kdf, MasterPasswordAuthenticationHash = NewMasterPasswordHash, Salt = salt };

    public static MasterPasswordUnlockDataRequestModel BuildUnlockData(KdfRequestModel kdf, string salt) =>
        new() { Kdf = kdf, MasterKeyWrappedUserKey = MasterKeyWrappedUserKey, Salt = salt };

    // Builds an (auth, unlock) pair where one side is perturbed so the agreement
    // validator must fire.
    //   mismatchKind:  "kdf" | "salt"  — which field disagrees
    //   perturbedSide: "auth" | "unlock" — which half carries the bad value
    //   salt:          the user's real salt, carried by every half that is not perturbed
    public static (MasterPasswordAuthenticationDataRequestModel auth, MasterPasswordUnlockDataRequestModel unlock)
        BuildMismatchedAuthAndUnlock(string mismatchKind, string perturbedSide, string salt)
    {
        var perturbedKdf = mismatchKind == "kdf"
            ? new KdfRequestModel { KdfType = KdfType.PBKDF2_SHA256, Iterations = 700_000 }
            : DefaultKdfRequest;
        var perturbedSalt = mismatchKind == "salt" ? "different-salt@bitwarden.com" : salt;

        return perturbedSide == "auth"
            ? (BuildAuthData(perturbedKdf, perturbedSalt), BuildUnlockData(DefaultKdfRequest, salt))
            : (BuildAuthData(DefaultKdfRequest, salt), BuildUnlockData(perturbedKdf, perturbedSalt));
    }
}
