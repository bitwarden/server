using Bit.Seeder.Models;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

public class PresetValidatorTests
{
    private const string _presetPrefix = "presets.";
    private const string _policyEnterprise = "features.policy-enterprise";
    private const string _ssoEnterprise = "features.sso-enterprise";
    private const string _sterlingCooper = "scale.md-balanced-sterling-cooper";
    private const string _defaultCollectionsEnterprise = "features.default-collections-enterprise";

    private readonly SeedReader _reader = new();

    [Fact]
    public void Policies_EnableAll_Throws()
    {
        var preset = ReadPreset(_policyEnterprise);
        preset = preset with { Policies = preset.Policies! with { EnableAll = true } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _policyEnterprise));

        Assert.Contains("'policies.enableAll'/'except', which are not supported", ex.Message);
    }

    [Fact]
    public void Policies_UnknownName_Throws()
    {
        var preset = ReadPreset(_policyEnterprise);
        preset = preset with { Policies = preset.Policies! with { Enable = [.. preset.Policies.Enable!, "require2fa"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _policyEnterprise));

        Assert.Contains("unknown policy 'require2fa'", ex.Message);
    }

    [Fact]
    public void Policies_EnabledAndDisabled_Throws()
    {
        var preset = ReadPreset(_policyEnterprise);
        preset = preset with { Policies = preset.Policies! with { Disable = ["masterPassword"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _policyEnterprise));

        Assert.Contains("lists MasterPassword in both 'policies.enable' and 'policies.disable'", ex.Message);
    }

    [Fact]
    public void Policies_RepeatedInOneList_Throws()
    {
        var preset = ReadPreset(_policyEnterprise);
        preset = preset with { Policies = preset.Policies! with { Enable = [.. preset.Policies.Enable!, "MasterPassword"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _policyEnterprise));

        Assert.Contains("lists MasterPassword more than once in 'policies.enable'", ex.Message);
    }

    [Fact]
    public void Policies_DataForUnlistedType_Throws()
    {
        var preset = ReadPreset(_policyEnterprise);
        preset = preset with
        {
            Policies = preset.Policies! with { Enable = preset.Policies.Enable!.Where(n => n != "masterPassword").ToList() }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _policyEnterprise));

        Assert.Contains("'policies.data' for MasterPassword, which is in neither", ex.Message);
    }

    [Fact]
    public void Policies_RequireSsoWithoutSingleOrg_Throws()
    {
        var preset = ReadPreset(_ssoEnterprise);
        preset = preset with { Policies = preset.Policies! with { Enable = ["requireSso"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _ssoEnterprise));

        Assert.Contains("enables RequireSso without singleOrg", ex.Message);
    }

    [Fact]
    public void Users_StatusMixWithRealisticStatusMix_Throws()
    {
        var preset = ReadPreset(_sterlingCooper);
        preset = preset with { Users = preset.Users! with { StatusMix = new SeedPresetStatusMix { Confirmed = 1 } } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _sterlingCooper));

        Assert.Contains("sets both 'users.statusMix' and 'users.realisticStatusMix'", ex.Message);
    }

    [Fact]
    public void Users_StatusMixWithoutPositiveWeight_Throws()
    {
        var preset = ReadPreset(_sterlingCooper);
        preset = preset with
        {
            Users = preset.Users! with { RealisticStatusMix = false, StatusMix = new SeedPresetStatusMix() }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _sterlingCooper));

        Assert.Contains("'users.statusMix' with no positive weight", ex.Message);
    }

    [Fact]
    public void Users_StatusMixWithNegativeWeight_Throws()
    {
        var preset = ReadPreset(_sterlingCooper);
        preset = preset with
        {
            Users = preset.Users! with
            {
                RealisticStatusMix = false,
                StatusMix = new SeedPresetStatusMix { Confirmed = 1, Accepted = -0.5 }
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _sterlingCooper));

        Assert.Contains("'users.statusMix' with a negative weight for accepted", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Users_AdminCountOutsideRange_Throws(int adminCount)
    {
        var preset = ReadPreset(_sterlingCooper);
        preset = preset with { Users = preset.Users! with { Count = 10, AdminCount = adminCount } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _sterlingCooper));

        Assert.Contains($"'users.adminCount' {adminCount}; it must be between 0 and 'users.count'", ex.Message);
    }

    [Fact]
    public void DefaultCollections_WithoutOrganizationDataOwnership_Throws()
    {
        var preset = ReadPreset(_defaultCollectionsEnterprise);
        preset = preset with { Policies = preset.Policies! with { Enable = ["singleOrg"] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _defaultCollectionsEnterprise));

        Assert.Contains("has 'defaultCollections' but doesn't enable organizationDataOwnership", ex.Message);
    }

    [Fact]
    public void DefaultCollections_NonEnterprisePlan_Throws()
    {
        var preset = ReadPreset(_defaultCollectionsEnterprise);
        preset = preset with { Organization = preset.Organization! with { PlanType = "teams-annually" } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _defaultCollectionsEnterprise));

        Assert.Contains("has 'defaultCollections' on plan 'teams-annually', which has no My Items", ex.Message);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void DefaultCollections_NonEmptyRateOutsideRange_Throws(double nonEmptyRate)
    {
        var preset = ReadPreset(_defaultCollectionsEnterprise);
        preset = preset with { DefaultCollections = preset.DefaultCollections! with { NonEmptyRate = nonEmptyRate } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _defaultCollectionsEnterprise));

        Assert.Contains("'defaultCollections.nonEmptyRate'", ex.Message);
        Assert.Contains("it must be between 0 and 1", ex.Message);
    }

    [Fact]
    public void DefaultCollections_NegativeDeletedRate_Throws()
    {
        var preset = ReadPreset(_defaultCollectionsEnterprise);
        preset = preset with { DefaultCollections = preset.DefaultCollections! with { DeletedRate = -0.1 } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _defaultCollectionsEnterprise));

        Assert.Contains("'defaultCollections.deletedRate'", ex.Message);
        Assert.Contains("it must be 0 or more", ex.Message);
    }

    [Fact]
    public void DefaultCollections_LargestEntryBelowOne_Throws()
    {
        var preset = ReadPreset(_defaultCollectionsEnterprise);
        preset = preset with { DefaultCollections = preset.DefaultCollections! with { Largest = [120, 0] } };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _defaultCollectionsEnterprise));

        Assert.Contains("'defaultCollections.largest' entries 0; every entry must be at least 1", ex.Message);
    }

    [Fact]
    public void DefaultCollections_SizesWithoutPositiveWeight_Throws()
    {
        var preset = ReadPreset(_defaultCollectionsEnterprise);
        preset = preset with
        {
            DefaultCollections = preset.DefaultCollections! with
            {
                Sizes = [new SeedPresetSizeBucket { Min = 1, Max = 5, Weight = 0 }]
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => PresetValidator.Validate(preset, _defaultCollectionsEnterprise));

        Assert.Contains("'defaultCollections.sizes' with no positive weight", ex.Message);
    }

    private SeedPreset ReadPreset(string presetName) => _reader.Read<SeedPreset>($"{_presetPrefix}{presetName}");
}
