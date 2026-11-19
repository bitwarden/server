using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

public class PresetLoaderTests
{
    private const string _presetPrefix = "presets.";

    public static TheoryData<string> EmbeddedPresetNames() => new(
        new SeedReader().ListAvailable()
            .Where(name => name.StartsWith(_presetPrefix))
            .Select(name => name[_presetPrefix.Length..]));

    [Theory]
    [MemberData(nameof(EmbeddedPresetNames))]
    public void EmbeddedPreset_RegistersRecipe(string presetName)
    {
        var services = new ServiceCollection();

        PresetLoader.RegisterRecipe(presetName, new SeedReader(), services);

        Assert.Contains(services, d => d.IsKeyedService && Equals(d.ServiceKey, presetName));
    }
}
