using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Auth.Models.Data;

public class DeviceAuthDetails : Device
{
    public bool IsTrusted { get; set; }
    public Guid? AuthRequestId { get; set; }
    public DateTime? AuthRequestCreatedAt { get; set; }

    /**
     * Constructor for EF response.
     */
    public DeviceAuthDetails(
        Device device,
        Guid? authRequestId,
        DateTime? authRequestCreationDate)
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
        AuthRequestId = authRequestId;
        AuthRequestCreatedAt = authRequestCreationDate;
    }


    /**
     * Constructor for dapper response.
     * Note: if the authRequestId or authRequestCreationDate is null it comes back as
     * an empty guid and a min value for datetime. That could change if the stored
     * procedure runs on a different kind of db.
     */
    public DeviceAuthDetails(
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
    {
        Id = id;
        Name = name;
        Type = (DeviceType)type;
        Identifier = identifier;
        CreationDate = creationDate;
        IsTrusted = new Device
        {
            Id = id,
            UserId = userId,
            Name = name,
            Type = (DeviceType)type,
            Identifier = identifier,
            PushToken = pushToken,
            RevisionDate = revisionDate,
            EncryptedUserKey = encryptedUserKey,
            EncryptedPublicKey = encryptedPublicKey,
            EncryptedPrivateKey = encryptedPrivateKey,
            Active = active
        }.IsTrusted();
        AuthRequestId = authRequestId != Guid.Empty ? authRequestId : null;
        AuthRequestCreatedAt =
            authRequestCreationDate != DateTime.MinValue ? authRequestCreationDate : null;
    }
}
