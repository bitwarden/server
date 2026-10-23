using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Core.Auth.UserFeatures.DeviceTrust.Data;

/// <summary>
/// Input for <see cref="IFinishSsoJitProvisionTrustedDeviceCommand"/>.
/// </summary>
public class FinishSsoJitProvisionTrustedDeviceData
{
    /// <summary>
    /// The account keys to store. A V2 state is written with the full account cryptographic state;
    /// a V1 state (public key encryption key pair only) is written as the legacy keypair.
    /// </summary>
    public required UserAccountKeysData AccountKeys { get; init; }

    /// <summary>
    /// Key id of the user key the account keys belong to. Only honored for V2 account keys.
    /// </summary>
    public KeyId? UserKeyId { get; init; }

    /// <summary>
    /// The organization the user was JIT provisioned into.
    /// </summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The user key encrypted with the organization public key, for admin account recovery enrollment.
    /// Required when the organization invite is accepted; ignored otherwise.
    /// For now, this is tied to accepting the organization invite.
    /// </summary>
    public string? ResetPasswordKey { get; init; }

    /// <summary>
    /// Keys to trust the current device with. Null when the user did not choose to trust the device.
    /// </summary>
    public TrustedDeviceKeysData? DeviceKeys { get; init; }
}
