using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
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

        services.AddScoped<IPolicyValidator, TwoFactorAuthenticationPolicyValidator>();
        services.AddScoped<IPolicyValidator, SingleOrgPolicyValidator>();
        services.AddScoped<IPolicyValidator, RequireSsoPolicyValidator>();
        services.AddScoped<IPolicyValidator, ResetPasswordPolicyValidator>();
        services.AddScoped<IPolicyValidator, MaximumVaultTimeoutPolicyValidator>();
        services.AddScoped<IPolicyValidator, FreeFamiliesForEnterprisePolicyValidator>();
    }
}
