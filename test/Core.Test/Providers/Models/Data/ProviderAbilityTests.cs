using Bit.Core.AdminConsole.Models.Data.Provider;
using Xunit;

using ProviderEntity = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Core.Test.AdminConsole.Models.Data.Provider;

public class ProviderAbilityTests
{
    [Fact]
    public void Constructor_MapsAllProperties()
    {
        var provider = new ProviderEntity
        {
            Id = Guid.NewGuid(),
            UseEvents = true,
            Enabled = true,
        };

        var ability = new ProviderAbility(provider);

        Assert.Equal(provider.Id, ability.Id);
        Assert.Equal(provider.UseEvents, ability.UseEvents);
        Assert.Equal(provider.Enabled, ability.Enabled);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        var provider = new ProviderEntity
        {
            Id = Guid.NewGuid(),
            UseEvents = false,
            Enabled = false,
        };

        var ability = new ProviderAbility(provider);

        Assert.Equal(provider.Id, ability.Id);
        Assert.False(ability.UseEvents);
        Assert.False(ability.Enabled);
    }
}
