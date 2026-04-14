using Bit.Core.Auth.Utilities;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Models.Data;

public class DeviceAuthDetails : Device
{
    public bool IsTrusted => DeviceExtensions.IsTrusted(this);
    public Guid? AuthRequestId { get; set; }
    public DateTime? AuthRequestCreationDate { get; set; }

    /**
     * Parameterless constructor for Dapper name-based mapping.
     */
    public DeviceAuthDetails() { }

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
        UserId = device.UserId;
        Name = device.Name;
        Type = device.Type;
        Identifier = device.Identifier;
        PushToken = device.PushToken;
        CreationDate = device.CreationDate;
        RevisionDate = device.RevisionDate;
        EncryptedUserKey = device.EncryptedUserKey;
        EncryptedPublicKey = device.EncryptedPublicKey;
        EncryptedPrivateKey = device.EncryptedPrivateKey;
        Active = device.Active;
        AuthRequestId = authRequestId;
        AuthRequestCreationDate = authRequestCreationDate;
    }
}
