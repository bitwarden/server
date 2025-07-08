// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Api.Response;

public class DeviceAuthRequestResponseModel : ResponseModel
{
    public DeviceAuthRequestResponseModel()
        : base("device") { }

    public static DeviceAuthRequestResponseModel From(DeviceAuthDetails deviceAuthDetails)
    {
        var converted = new DeviceAuthRequestResponseModel
        {
            Id = deviceAuthDetails.Id,
            Name = deviceAuthDetails.Name,
            Type = deviceAuthDetails.Type,
            Identifier = deviceAuthDetails.Identifier,
            CreationDate = deviceAuthDetails.CreationDate,
            IsTrusted = deviceAuthDetails.IsTrusted,
            EncryptedPublicKey = deviceAuthDetails.EncryptedPublicKey,
            EncryptedUserKey = deviceAuthDetails.EncryptedUserKey
        };

        if (deviceAuthDetails.AuthRequestId != null && deviceAuthDetails.AuthRequestCreatedAt != null)
        {
            converted.DevicePendingAuthRequest = new PendingAuthRequest
            {
                Id = (Guid)deviceAuthDetails.AuthRequestId,
                CreationDate = (DateTime)deviceAuthDetails.AuthRequestCreatedAt
            };
        }

        return converted;
    }

    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public string Identifier { get; set; }
    public DateTime CreationDate { get; set; }
    public bool IsTrusted { get; set; }
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }

    public PendingAuthRequest DevicePendingAuthRequest { get; set; }

    public class PendingAuthRequest
    {
        public Guid Id { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
