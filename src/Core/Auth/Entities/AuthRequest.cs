// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Entities;

public class AuthRequest : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Enums.AuthRequestType Type { get; set; }
    [MaxLength(50)]
    public string RequestDeviceIdentifier { get; set; }
    public DeviceType RequestDeviceType { get; set; }
    [MaxLength(50)]
    public string RequestIpAddress { get; set; }
    /// <summary>
    /// This country name is populated through a header value fetched from the ISO-3166 country code.
    /// It will always be the English short form of the country name. The length should never be over 200 characters.
    /// </summary>
    [MaxLength(200)]
    public string RequestCountryName { get; set; }
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
        return ResponseDate.HasValue || AuthenticationDate.HasValue || IsExpired();
    }

    public bool IsExpired()
    {
        return GetExpirationDate() < DateTime.UtcNow;
    }

    public bool IsValidForAuthentication(Guid userId,
        string password)
    {
        return ResponseDate.HasValue // it’s been responded to
               && Approved == true // it was approved
               && !IsExpired() // it's not expired
               && Type == AuthRequestType.AuthenticateAndUnlock // it’s an authN request
               && !AuthenticationDate.HasValue // it was not already used for authN
               && UserId == userId // it belongs to the user
               && CoreHelpers.FixedTimeEquals(AccessCode, password);  // the access code matches the password
    }

    public DateTime GetExpirationDate()
    {
        // TODO: PM-24252 - this should reference PasswordlessAuthSettings.UserRequestExpiration
        return CreationDate.AddMinutes(15);
    }
}
