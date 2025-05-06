﻿using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
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
    }

    private static void AddPolicyValidators(this IServiceCollection services)
    {
        services.AddScoped<IPolicyValidator, TwoFactorAuthenticationPolicyValidator>();
        services.AddScoped<IPolicyValidator, SingleOrgPolicyValidator>();
        services.AddScoped<IPolicyValidator, RequireSsoPolicyValidator>();
        services.AddScoped<IPolicyValidator, ResetPasswordPolicyValidator>();
        services.AddScoped<IPolicyValidator, MaximumVaultTimeoutPolicyValidator>();
        services.AddScoped<IPolicyValidator, FreeFamiliesForEnterprisePolicyValidator>();
    }

    private static void AddPolicyRequirements(this IServiceCollection services)
    {
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, DisableSendPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, SendOptionsPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, ResetPasswordPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, PersonalOwnershipPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, RequireSsoPolicyRequirementFactory>();
    }
}
