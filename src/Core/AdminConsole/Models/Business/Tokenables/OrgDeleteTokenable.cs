using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Models.Business.Tokenables;

public class OrgDeleteTokenable : Tokens.ExpiringTokenable
{
    public const string ClearTextPrefix = "";
    public const string DataProtectorPurpose = "OrgDeleteDataProtector";
    public const string TokenIdentifier = "OrgDelete";
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }

    [JsonConstructor]
    public OrgDeleteTokenable(DateTime expirationDate)
    {
        ExpirationDate = expirationDate;
    }

    public OrgDeleteTokenable(Organization organization, int hoursTillExpiration)
    {
        Id = organization.Id;
        ExpirationDate = DateTime.UtcNow.AddHours(hoursTillExpiration);
    }

    public bool IsValid(Organization organization)
    {
        return Id == organization.Id;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && Id != default;
}
