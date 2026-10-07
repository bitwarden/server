using System.Diagnostics.CodeAnalysis;
using Bit.Core.KeyManagement.UserKey.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.KeyManagement.Models.Responses;

public class TrustedDeviceKeyDataResponseModel : ResponseModel
{
    private const string _objectName = "trustedDeviceKeyData";

    [SetsRequiredMembers]
    public TrustedDeviceKeyDataResponseModel(TrustedDeviceKeyData data, string obj = _objectName) : base(obj)
    {
        ArgumentNullException.ThrowIfNull(data);

        Id = data.Id;
        EncryptedPublicKey = data.EncryptedPublicKey;
        EncryptedUserKey = data.EncryptedUserKey;
    }

    public TrustedDeviceKeyDataResponseModel() : base(_objectName)
    {
    }

    public Guid Id { get; set; }
    public required string EncryptedPublicKey { get; set; }
    public required string EncryptedUserKey { get; set; }
}
