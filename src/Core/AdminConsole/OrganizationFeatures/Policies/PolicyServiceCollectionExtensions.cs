using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
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

        services.AddScoped<IPolicyDefinition, TwoFactorAuthenticationPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, MasterPasswordPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, PasswordGeneratorPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, SingleOrgPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, RequireSsoPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, PersonalOwnershipPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, DisableSendPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, SendOptionsPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, ResetPasswordPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, MaximumVaultTimeoutPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, DisablePersonalVaultExportPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, ActivateAutofillPolicyDefinition>();
        services.AddScoped<IPolicyDefinition, AutomaticAppLogInPolicyDefinition>();
    }
}
