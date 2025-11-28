using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
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
        services.AddScoped<IVNextSavePolicyCommand, VNextSavePolicyCommand>();
        services.AddScoped<IPolicyRequirementQuery, PolicyRequirementQuery>();
        services.AddScoped<IPolicyEventHandlerFactory, PolicyEventHandlerHandlerFactory>();

        services.AddPolicyValidators();
        services.AddPolicyRequirements();
        services.AddPolicySideEffects();
        services.AddPolicyUpdateEvents();
    }

    [Obsolete("Use AddPolicyUpdateEvents instead.")]
    private static void AddPolicyValidators(this IServiceCollection services)
    {
        services.AddScoped<IPolicyValidator, TwoFactorAuthenticationPolicyValidator>();
        services.AddScoped<IPolicyValidator, SingleOrgPolicyValidator>();
        services.AddScoped<IPolicyValidator, RequireSsoPolicyValidator>();
        services.AddScoped<IPolicyValidator, ResetPasswordPolicyValidator>();
        services.AddScoped<IPolicyValidator, MaximumVaultTimeoutPolicyValidator>();
        services.AddScoped<IPolicyValidator, UriMatchDefaultPolicyValidator>();
        services.AddScoped<IPolicyValidator, FreeFamiliesForEnterprisePolicyValidator>();
    }

    [Obsolete("Use AddPolicyUpdateEvents instead.")]
    private static void AddPolicySideEffects(this IServiceCollection services)
    {
        services.AddScoped<IPostSavePolicySideEffect, OrganizationDataOwnershipPolicyValidator>();
    }

    private static void AddPolicyUpdateEvents(this IServiceCollection services)
    {
        services.AddScoped<IPolicyUpdateEvent, RequireSsoPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, TwoFactorAuthenticationPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, SingleOrgPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, ResetPasswordPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, MaximumVaultTimeoutPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, FreeFamiliesForEnterprisePolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, OrganizationDataOwnershipPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, UriMatchDefaultPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, BlockClaimedDomainAccountCreationPolicyValidator>();
        services.AddScoped<IPolicyUpdateEvent, AutomaticUserConfirmationPolicyEventHandler>();
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
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, SingleOrganizationPolicyRequirementFactory>();
    }
}
