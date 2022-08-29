using Bit.Core.Entities.Provider;

namespace Bit.Core.Models.Data;

public class ProviderAbility
{
    public ProviderAbility() { }

    public ProviderAbility(Provider provider)
    {
        Id = provider.Id;
        UseEvents = provider.UseEvents;
        Enabled = provider.Enabled;
    }

    public Guid Id { get; set; }
    public bool UseEvents { get; set; }
    public bool Enabled { get; set; }
}
