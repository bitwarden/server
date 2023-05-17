using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Models.Api;

#nullable enable

namespace Bit.Core.Auth.Models.Api.Response;

[JsonConverter(typeof(UserDecryptionOptionConverter))]
public abstract class UserDecryptionOption : ResponseModel
{
    protected UserDecryptionOption(string type) : base(type)
    {
    }
}

public class MasterPasswordUserDecryptionOption : UserDecryptionOption
{
    public MasterPasswordUserDecryptionOption()
        : base("masterPasswordOption")
    { }
}

public class TrustedDeviceUserDecryptionOption : UserDecryptionOption
{
    public bool HasAdminApproval { get; }

    public TrustedDeviceUserDecryptionOption(bool hasAdminApproval)
        : base("trustedDeviceOption")
    {
        HasAdminApproval = hasAdminApproval;
    }
}

public class KeyConnectorUserDecryptionOption : UserDecryptionOption
{
    public string KeyConnectorUrl { get; }

    public KeyConnectorUserDecryptionOption(string keyConnectorUrl)
        : base("keyConnectorOption")
    {
        KeyConnectorUrl = keyConnectorUrl;
    }
}

/// <summary>
/// A JsonConverter to handle polymorphic serialization
/// </summary>
/// <remarks>
/// IdentityServer4 utilized System.Text.Json for their response <see href="https://github.com/IdentityServer/IdentityServer4/blob/4dc10e665f5ede63274427036c25cdc216130eb9/src/IdentityServer4/src/Endpoints/Results/TokenResult.cs" />
/// </remarks>
public class UserDecryptionOptionConverter : JsonConverter<UserDecryptionOption>
{
    public override UserDecryptionOption? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotImplementedException($"It's not expected to need to read {typeToConvert.FullName}");
    public override void Write(Utf8JsonWriter writer, UserDecryptionOption value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case MasterPasswordUserDecryptionOption masterPasswordOption:
                JsonSerializer.Serialize(writer, masterPasswordOption, typeof(MasterPasswordUserDecryptionOption), options);
                break;
            case TrustedDeviceUserDecryptionOption trustedDeviceUserDecryptionOption:
                JsonSerializer.Serialize(writer, trustedDeviceUserDecryptionOption, typeof(TrustedDeviceUserDecryptionOption), options);
                break;
            case KeyConnectorUserDecryptionOption keyConnectorOption:
                JsonSerializer.Serialize(writer, keyConnectorOption, typeof(KeyConnectorUserDecryptionOption), options);
                break;
            default:
                Debug.Fail($"Unknown inherited class of type {value.GetType().FullName}");
                writer.WriteNullValue();
                break;
        }
    }
}
