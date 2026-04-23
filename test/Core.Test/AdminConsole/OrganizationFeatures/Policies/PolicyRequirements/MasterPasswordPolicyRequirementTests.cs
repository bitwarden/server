using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class MasterPasswordPolicyRequirementTests
{
    [Fact]
    public void WithNoPolicies_EnforcedOptionsIsNull()
    {
        var sut = new MasterPasswordPolicyRequirement([]);

        Assert.Null(sut.EnforcedOptions);
    }

    [Fact]
    public void WithSinglePolicy_ReturnsEnforcedOptions()
    {
        var data = new MasterPasswordPolicyData { MinLength = 12, RequireUpper = true };
        var sut = new MasterPasswordPolicyRequirement(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(data)
            }
        ]);

        Assert.NotNull(sut.EnforcedOptions);
        Assert.Equal(12, sut.EnforcedOptions.MinLength);
        Assert.True(sut.EnforcedOptions.RequireUpper);
    }

    [Fact]
    public void WithMultiplePolicies_TakesMaxMinComplexity()
    {
        var sut = new MasterPasswordPolicyRequirement(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData { MinComplexity = 2 })
            },
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData { MinComplexity = 4 })
            }
        ]);

        Assert.NotNull(sut.EnforcedOptions);
        Assert.Equal(4, sut.EnforcedOptions.MinComplexity);
    }

    [Fact]
    public void WithMultiplePolicies_TakesMaxMinLength()
    {
        var sut = new MasterPasswordPolicyRequirement(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData { MinLength = 12 })
            },
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData { MinLength = 20 })
            }
        ]);

        Assert.NotNull(sut.EnforcedOptions);
        Assert.Equal(20, sut.EnforcedOptions.MinLength);
    }

    [Fact]
    public void WithMultiplePolicies_OrsAllBooleanFlags()
    {
        var sut = new MasterPasswordPolicyRequirement(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData { RequireUpper = true })
            },
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData { RequireSpecial = true })
            }
        ]);

        Assert.NotNull(sut.EnforcedOptions);
        Assert.True(sut.EnforcedOptions.RequireUpper);
        Assert.True(sut.EnforcedOptions.RequireSpecial);
    }

    [Fact]
    public void WithAllNullOptions_EnforcedOptionsIsNull()
    {
        // A policy saved with no options set produces an all-null MasterPasswordPolicyData.
        // EnforcedOptions should be null so callers can treat it as "no policy enforced".
        var sut = new MasterPasswordPolicyRequirement(
        [
            new PolicyDetails
            {
                PolicyType = PolicyType.MasterPassword,
                PolicyData = System.Text.Json.JsonSerializer.Serialize(new MasterPasswordPolicyData())
            }
        ]);

        Assert.Null(sut.EnforcedOptions);
    }
}
