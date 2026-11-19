using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.Models.Business.Tokenables;

public class OrgDeleteTokenable : Tokens.ExpiringTokenable
{
    public const string ClearTextPrefix = "";
    public const string DataProtectorPurpose = "OrgDeleteDataProtector";
    public const string TokenIdentifier = "OrgDelete";
    private const int ExpirationHours = 48;
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }

    public OrgDeleteTokenable() { }

    public OrgDeleteTokenable(Organization organization, TimeProvider timeProvider)
    {
        Id = organization.Id;
        ExpirationDate = timeProvider.GetUtcNow().UtcDateTime.AddHours(ExpirationHours);
    }

    public bool IsValid(Organization organization)
    {
        return Id == organization.Id;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && Id != default;
}
