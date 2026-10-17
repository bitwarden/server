using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

/// <summary>
/// Every embedded preset passes <see cref="PresetValidator"/>, and the rules that stop settings from being silently
/// ignored or producing states the server can't reach reject what they should.
/// </summary>
public sealed class PresetValidatorTests
{
    private const string _shaped = "scale.xl-migrated-cyberdyne";
    private static readonly SeedReader _reader = new();

    public static TheoryData<string> AllPresets()
    {
        var data = new TheoryData<string>();
        foreach (var name in _reader.ListAvailable().Where(n => n.StartsWith("presets.")))
        {
            data.Add(name["presets.".Length..]);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllPresets))]
    public void EmbeddedPreset_PassesValidation(string name)
    {
        PresetValidator.Validate(Read(name), name);
    }

    [Fact]
    public void AccessShape_WithDensityMembership_Throws()
    {
        var preset = Read(_shaped);
        preset = preset with { Density = preset.Density! with { Membership = new SeedPresetMembership { Shape = "powerLaw" } } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
        Assert.Contains("density.membership", ex.Message);
    }

    [Fact]
    public void AccessShape_WithGroupsButNoShapeGroups_Throws()
    {
        var preset = Read(_shaped);
        preset = preset with { AccessShape = preset.AccessShape! with { Groups = null } };

        Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
    }

    [Fact]
    public void Policies_EnableAll_Throws()
    {
        var preset = Read(_shaped);
        preset = preset with { Policies = preset.Policies! with { EnableAll = true } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
        Assert.Contains("enableAll", ex.Message);
    }

    [Fact]
    public void Policies_EnabledAndDisabled_Throws()
    {
        var preset = Read(_shaped);
        preset = preset with { Policies = preset.Policies! with { Disable = ["singleOrg"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
        Assert.Contains("SingleOrg", ex.Message);
    }

    [Fact]
    public void Policies_DataForUnlistedType_Throws()
    {
        var preset = Read(_shaped);
        var enable = preset.Policies!.Enable!.Where(p => p != "sendOptions").ToList();
        preset = preset with { Policies = preset.Policies with { Enable = enable } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
        Assert.Contains("SendOptions", ex.Message);
    }

    [Fact]
    public void Policies_RequireSsoWithoutSingleOrg_Throws()
    {
        var preset = Read("features.sso-enterprise");
        preset = preset with { Policies = preset.Policies! with { Enable = ["requireSso"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, "features.sso-enterprise"));
        Assert.Contains("singleOrg", ex.Message);
    }

    [Fact]
    public void MyItems_WithoutOwnershipPolicy_Throws()
    {
        var preset = Read(_shaped);
        var enable = preset.Policies!.Enable!.Where(p => p != "organizationDataOwnership").ToList();
        preset = preset with { Policies = preset.Policies with { Enable = enable } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
        Assert.Contains("organizationDataOwnership", ex.Message);
    }

    [Fact]
    public void MyItems_OnTeamsPlan_Throws()
    {
        var preset = Read(_shaped);
        preset = preset with { Organization = preset.Organization! with { PlanType = "teams-annually" } };

        Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _shaped));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(99, 100)]
    [InlineData(120, 100)]
    public void ShapeTargets_WithinTolerance_DoesNotThrow(long actual, long target)
    {
        ShapeTargets.EnsureReached("rows", actual, target);
    }

    [Fact]
    public void ShapeTargets_Shortfall_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ShapeTargets.EnsureReached("rows", 98, 100));
    }

    private static SeedPreset Read(string name) => _reader.Read<SeedPreset>($"presets.{name}");
}
