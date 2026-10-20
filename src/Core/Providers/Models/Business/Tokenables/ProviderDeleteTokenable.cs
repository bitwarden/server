using Newtonsoft.Json;

namespace Bit.Core.Providers.Models.Business.Tokenables;

public class ProviderDeleteTokenable : Tokens.ExpiringTokenable
{
    public const string ClearTextPrefix = "BwProviderId";
    public const string DataProtectorPurpose = "ProviderDeleteDataProtector";
    public const string TokenIdentifier = "ProviderDelete";
    public string Identifier { get; set; } = TokenIdentifier;
    public Guid Id { get; set; }

    [JsonConstructor]
    public ProviderDeleteTokenable()
    {

    }

    [JsonConstructor]
    public ProviderDeleteTokenable(DateTime expirationDate)
    {
        ExpirationDate = expirationDate;
    }

    public ProviderDeleteTokenable(Entities.Provider provider, int hoursTillExpiration)
    {
        Id = provider.Id;
        ExpirationDate = DateTime.UtcNow.AddHours(hoursTillExpiration);
    }

    public bool IsValid(Entities.Provider provider)
    {
        return Id == provider.Id;
    }

    protected override bool TokenIsValid() => Identifier == TokenIdentifier && Id != default;
}
