using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public static class PolicyServiceCollectionExtensions
{
    public static void AddPolicyServices(this IServiceCollection services)
    {
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddScoped<ISavePolicyCommand, SavePolicyCommand>();
        services.AddScoped<IPolicyRequirementQuery, PolicyRequirementQuery>();

        services.AddPolicyValidators();
        services.AddPolicyRequirements();
        services.AddPolicySideEffects();
    }

    private static void AddPolicyValidators(this IServiceCollection services)
    {
        services.AddScoped<IPolicyValidator, TwoFactorAuthenticationPolicyValidator>();
        services.AddScoped<IPolicyValidator, SingleOrgPolicyValidator>();

        services.AddScoped<IPolicyValidator, ResetPasswordPolicyValidator>();

        services.AddScoped<IPolicyValidator, FreeFamiliesForEnterprisePolicyValidator>();

        services.AddScoped<IEnforceDependentPoliciesEvent, RequireSsoOnPolicyEventEventEnsureEventValidator>();
        services.AddScoped<IPolicyValidationEvent, RequireSsoOnPolicyEventEventEnsureEventValidator>();
        services.AddScoped<IOnPolicyPostSaveEvent, RequireSsoOnPolicyEventEventEnsureEventValidator>();

        services.AddScoped<IEnforceDependentPoliciesEvent, MaximumVaultTimeoutPolicyEventEventValidator>();
    }

    private static void AddPolicySideEffects(this IServiceCollection services)
    {
        services.AddScoped<IPostSavePolicySideEffect, OrganizationDataOwnershipPolicyValidator>();
    }

    private static void AddPolicyRequirements(this IServiceCollection services)
    {
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, DisableSendPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, SendOptionsPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, ResetPasswordPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, OrganizationDataOwnershipPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, RequireSsoPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, RequireTwoFactorPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, MasterPasswordPolicyRequirementFactory>();
    }
}
