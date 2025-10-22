using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public class UriMatchDefaultPolicyValidatorTests
{
    private readonly UriMatchDefaultPolicyValidator _validator = new();

    [Fact]
    // Test that the Type property returns the correct PolicyType for this validator
    public void Type_ReturnsUriMatchDefaults()
    {
        Assert.Equal(PolicyType.UriMatchDefaults, _validator.Type);
    }

    [Fact]
    // Test that the RequiredPolicies property returns exactly one policy (SingleOrg) as a prerequisite
    // for enabling the UriMatchDefaults policy, ensuring proper policy dependency enforcement
    public void RequiredPolicies_ReturnsSingleOrgPolicy()
    {
        var requiredPolicies = _validator.RequiredPolicies.ToList();

        Assert.Single(requiredPolicies);
        Assert.Contains(PolicyType.SingleOrg, requiredPolicies);
    }

    [Fact]
    // Happy path test for ValidateAsync, returns empty string indicating no validation errors
    public async Task ValidateAsync_ReturnsEmptyString()
    {
        var policyUpdate = new PolicyUpdate { Type = PolicyType.UriMatchDefaults };
        var currentPolicy = new Policy { Type = PolicyType.UriMatchDefaults };
        var result = await _validator.ValidateAsync(policyUpdate, currentPolicy);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    // Test that ValidateAsync handles the case where no existing policy exists (null currentPolicy)
    // and still returns an empty string, indicating no validation errors for a new policy
    public async Task ValidateAsync_WithNullCurrentPolicy_ReturnsEmptyString()
    {
        var policyUpdate = new PolicyUpdate { Type = PolicyType.UriMatchDefaults };
        var result = await _validator.ValidateAsync(policyUpdate, null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    // Test that OnSaveSideEffectsAsync completes successfully without performing any side effects
    // when both policyUpdate and currentPolicy are provided, verifying the method returns Task.CompletedTask
    public async Task OnSaveSideEffectsAsync_CompletesSuccessfully()
    {
        var policyUpdate = new PolicyUpdate { Type = PolicyType.UriMatchDefaults };
        var currentPolicy = new Policy { Type = PolicyType.UriMatchDefaults };
        var task = _validator.OnSaveSideEffectsAsync(policyUpdate, currentPolicy);

        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }

    [Fact]
    // Test that OnSaveSideEffectsAsync handles the null currentPolicy scenario (new policy creation)
    // and completes successfully without throwing exceptions or performing side effects
    public async Task OnSaveSideEffectsAsync_WithNullCurrentPolicy_CompletesSuccessfully()
    {
        var policyUpdate = new PolicyUpdate { Type = PolicyType.UriMatchDefaults };
        var task = _validator.OnSaveSideEffectsAsync(policyUpdate, null);

        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }
}
