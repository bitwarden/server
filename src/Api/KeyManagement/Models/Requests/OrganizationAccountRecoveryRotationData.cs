namespace Bit.Api.KeyManagement.Models.Requests;

/// <summary>
/// The account recovery keys submitted for a key rotation, together with whether the rotation carries a
/// V2 upgrade token. The token changes what the server accepts, so the validator needs to see both.
/// </summary>
public class OrganizationAccountRecoveryRotationData
{
    public required IEnumerable<OrganizationUserAccountRecoveryRequestModel> AccountRecoveryUnlockData { get; init; }
    public required bool HasV2UpgradeToken { get; init; }
}
