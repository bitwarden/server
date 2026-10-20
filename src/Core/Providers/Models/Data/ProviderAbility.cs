namespace Bit.Core.Providers.Models.Data;

public class ProviderAbility
{
    public ProviderAbility() { }

    public ProviderAbility(Entities.Provider provider)
    {
        Id = provider.Id;
        UseEvents = provider.UseEvents;
        Enabled = provider.Enabled;
    }

    public Guid Id { get; set; }
    public bool UseEvents { get; set; }
    public bool Enabled { get; set; }
}
