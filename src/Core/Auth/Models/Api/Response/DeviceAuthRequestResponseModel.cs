using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Core.Auth.Models.Api.Response;

public class DeviceAuthRequestResponseModel : ResponseModel
{
    public DeviceAuthRequestResponseModel(
        Device device,
        Guid authRequestId,
        DateTime authRequestCreationDate)
        : base("device")
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        Id = device.Id;
        Name = device.Name;
        Type = device.Type;
        Identifier = device.Identifier;
        CreationDate = device.CreationDate;
        IsTrusted = device.IsTrusted();
        if (authRequestId != Guid.Empty && authRequestCreationDate != DateTime.MinValue)
        {
            DevicePendingAuthRequest = new PendingAuthRequest { Id = authRequestId, CreationDate = authRequestCreationDate };
        }
    }

    /**
     * Is there a better way to do this for Dapper so that I don't need to explicitly
     * enumerate all the properties in the constructor for mapping?
     */
    public DeviceAuthRequestResponseModel(
        Guid id,
        Guid userId,
        string name,
        short type,
        string identifier,
        string pushToken,
        DateTime creationDate,
        DateTime revisionDate,
        string encryptedUserKey,
        string encryptedPublicKey,
        string encryptedPrivateKey,
        bool active,
        Guid authRequestId,
        DateTime authRequestCreationDate)
        : base("device")
    {
        Id = id;
        Name = name;
        Type = (DeviceType)type;
        Identifier = identifier;
        CreationDate = creationDate;
        IsTrusted = new Device
        {
            EncryptedUserKey = encryptedUserKey,
            EncryptedPublicKey = encryptedPublicKey,
            EncryptedPrivateKey = encryptedPrivateKey,
        }.IsTrusted();

        if (authRequestId != Guid.Empty && authRequestCreationDate != DateTime.MinValue)
        {
            DevicePendingAuthRequest = new PendingAuthRequest
            {
                Id = authRequestId,
                CreationDate = authRequestCreationDate,
            };
        }
    }


    public Guid Id { get; set; }
    public string Name { get; set; }
    public DeviceType Type { get; set; }
    public string Identifier { get; set; }
    public DateTime CreationDate { get; set; }
    public bool IsTrusted { get; set; }

    public PendingAuthRequest DevicePendingAuthRequest { get; set; }

    public class PendingAuthRequest
    {
        public Guid Id { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
