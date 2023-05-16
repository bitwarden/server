using Bit.Core.Models.Api;

#nullable enable

namespace Bit.Core.Auth.Models.Api.Response;

public abstract class MemberDecryptionOptions : ResponseModel
{
    protected MemberDecryptionOptions(string type) : base(type)
    {
    }
}

public class TrustedDeviceMemberDecryptionOptions : MemberDecryptionOptions
{
    public TrustedDeviceMemberDecryptionOptions(
        bool hasMasterPassword)
        : base("trustedDeviceOptions")
    {
        HasMasterPassword = hasMasterPassword;
    }

    public bool HasMasterPassword { get; }
}

public class KeyConnectorMemberDecryptionOptions : MemberDecryptionOptions
{
    public KeyConnectorMemberDecryptionOptions(string keyConnectorUrl)
        : base("keyConnectorOptions")
    {
        KeyConnectorUrl = keyConnectorUrl;
    }

    public string KeyConnectorUrl { get; }
}
