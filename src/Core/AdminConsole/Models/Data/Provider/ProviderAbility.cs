namespace Bit.Core.AdminConsole.Models.Data.Provider;

public class ProviderAbility
{
    public ProviderAbility() { }

    public ProviderAbility(Entities.Provider.Provider provider)
    {
        Id = provider.Id;
        UseEvents = provider.UseEvents;
        Enabled = provider.Enabled;
    }

    public Guid Id { get; set; }
    public bool UseEvents { get; set; }
    public bool Enabled { get; set; }
}
