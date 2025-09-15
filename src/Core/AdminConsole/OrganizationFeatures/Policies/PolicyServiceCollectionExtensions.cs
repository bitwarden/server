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
        services.AddScoped<IPolicyEventHandlerFactory, PolicyEventHandlerHandlerFactory>();

        services.AddPolicyValidators();
        services.AddPolicyRequirements();
        services.AddPolicySideEffects();
    }

    private static void AddPolicyValidators(this IServiceCollection services)
    {
        services.AddScoped<IPolicyValidator, SingleOrgPolicyValidator>();
        services.AddScoped<IPolicyValidator, ResetPasswordPolicyValidator>();
        services.AddScoped<IPolicyValidator, FreeFamiliesForEnterprisePolicyValidator>();

        services.AddScoped<IEnforceDependentPoliciesEvent, RequireSsoPolicyHandler>();
        services.AddScoped<IEnforceDependentPoliciesEvent, MaximumVaultTimeoutPolicyEventEventValidator>();

        services.AddScoped<IPolicyValidationEvent, RequireSsoPolicyHandler>();
        services.AddScoped<IOnPolicyPreSaveEvent, TwoFactorAuthenticationPolicyHandler>();
    }

    private static void AddPolicySideEffects(this IServiceCollection services)
    {
        services.AddScoped<IPostSavePolicySideEffect, OrganizationDataOwnershipPolicyHandler>();
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
