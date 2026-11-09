namespace Bit.Core.AdminConsole.Models.Business.Tokenables;

public class ProviderDeleteTokenable : Tokens.ExpiringTokenable
{
    public const string ClearTextPrefix = "BwProviderId";
    public const string DataProtectorPurpose = "ProviderDeleteDataProtector";
    public const string TokenIdentifier = "ProviderDelete";
    private const int ExpirationHours = 48;
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }

    public ProviderDeleteTokenable() { }

    public ProviderDeleteTokenable(Entities.Provider.Provider provider, TimeProvider timeProvider)
    {
        Id = provider.Id;
        ExpirationDate = timeProvider.GetUtcNow().UtcDateTime.AddHours(ExpirationHours);
    }

    public bool IsValid(Entities.Provider.Provider provider)
    {
        return Id == provider.Id;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && Id != default;
}
