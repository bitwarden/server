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
        Name = device.Name;
        Type = device.Type;
        Identifier = device.Identifier;
        CreationDate = device.CreationDate;
        EncryptedPublicKey = device.EncryptedPublicKey;
        EncryptedUserKey = device.EncryptedUserKey;
        AuthRequestId = authRequestId;
        AuthRequestCreationDate = authRequestCreationDate;
    }
}
