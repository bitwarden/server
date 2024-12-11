using System.Text.Json.Serialization;
using Bit.Core.Auth.Entities;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class EmergencyAccessInviteTokenable : Tokens.ExpiringTokenable
{
    public const string ClearTextPrefix = "";
    public const string DataProtectorPurpose = "EmergencyAccessServiceDataProtector";
    public const string TokenIdentifier = "EmergencyAccessInvite";
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }
    public string Email { get; set; }

    [JsonConstructor]
    public EmergencyAccessInviteTokenable(DateTime expirationDate)
    {
        ExpirationDate = expirationDate;
    }

    public EmergencyAccessInviteTokenable(EmergencyAccess user, int hoursTillExpiration)
    {
        Id = user.Id;
        Email = user.Email;
        ExpirationDate = DateTime.UtcNow.AddHours(hoursTillExpiration);
    }

    public bool IsValid(Guid id, string email)
    {
        return Id == id && Email.Equals(email, StringComparison.InvariantCultureIgnoreCase);
    }

    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier && Id != default && !string.IsNullOrWhiteSpace(Email);
}
