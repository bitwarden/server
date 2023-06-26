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
    /// 
    /// </summary>
    public bool HasMasterPassword { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TrustedDeviceUserDecryptionOption? TrustedDeviceOption { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KeyConnectorUserDecryptionOption? KeyConnectorOption { get; set; }
}

public class TrustedDeviceUserDecryptionOption
{
    public bool HasAdminApproval { get; }

    public TrustedDeviceUserDecryptionOption(bool hasAdminApproval)
    {
        HasAdminApproval = hasAdminApproval;
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
