using System.Text.Json.Serialization;
using Bit.Core.Models.Api;

#nullable enable

namespace Bit.Core.Auth.Models.Api.Response;

public class UserDecryptionOptions : ResponseModel
{
    public UserDecryptionOptions() : base("userDecryptionOptions")
    {
    }

    /// <summary>
    /// Gets or sets whether the current user has a master password that can be used to decrypt their vault.
    /// </summary>
    public bool HasMasterPassword { get; set; }

    /// <summary>
    /// Gets or sets information regarding this users trusted device decryption setup.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrustedDeviceUserDecryptionOption? TrustedDeviceOption { get; set; }

    /// <summary>
    /// Gets or set information about the current users KeyConnector setup.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KeyConnectorUserDecryptionOption? KeyConnectorOption { get; set; }
}

public class TrustedDeviceUserDecryptionOption
{
    public bool HasAdminApproval { get; }
    public bool HasLoginApprovingDevice { get; }
    public string? EncryptedPrivateKey { get; }
    public string? EncryptedUserKey { get; }

    public TrustedDeviceUserDecryptionOption(bool hasAdminApproval,
        bool hasLoginApprovingDevice,
        string? encryptedPrivateKey,
        string? encryptedUserKey)
    {
        HasAdminApproval = hasAdminApproval;
        HasLoginApprovingDevice = hasLoginApprovingDevice;
        EncryptedPrivateKey = encryptedPrivateKey;
        EncryptedUserKey = encryptedUserKey;
    }
}

public class KeyConnectorUserDecryptionOption
{
    public string KeyConnectorUrl { get; }

    public KeyConnectorUserDecryptionOption(string keyConnectorUrl)
    {
        KeyConnectorUrl = keyConnectorUrl;
    }
}
