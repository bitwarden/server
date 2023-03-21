using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class AuthRequest : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Enums.AuthRequestType Type { get; set; }
    [MaxLength(50)]
    public string RequestDeviceIdentifier { get; set; }
    public DeviceType RequestDeviceType { get; set; }
    [MaxLength(50)]
    public string RequestIpAddress { get; set; }
    public Guid? ResponseDeviceId { get; set; }
    [MaxLength(25)]
    public string AccessCode { get; set; }
    public string PublicKey { get; set; }
    public string Key { get; set; }
    public string MasterPasswordHash { get; set; }
    public bool? Approved { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseDate { get; set; }
    public DateTime? AuthenticationDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public bool IsSpent()
    {
        return ResponseDate.HasValue || AuthenticationDate.HasValue || GetExpirationDate() < DateTime.UtcNow;
    }

    public DateTime GetExpirationDate()
    {
        return CreationDate.AddMinutes(15);
    }
}
