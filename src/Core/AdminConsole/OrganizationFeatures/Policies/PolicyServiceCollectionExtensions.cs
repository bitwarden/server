using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Enforcement.AutoConfirm;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyEventHandlers;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
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
        services.AddScoped<IPolicyQuery, PolicyQuery>();
        services.AddScoped<IPolicyEventHandlerFactory, PolicyEventHandlerHandlerFactory>();

        services.AddScoped<IAutomaticUserConfirmationPolicyEnforcementValidator, AutomaticUserConfirmationPolicyEnforcementValidator>();
        services.AddScoped<IAutomaticUserConfirmationOrganizationPolicyComplianceValidator, AutomaticUserConfirmationOrganizationPolicyComplianceValidator>();

        services.AddPolicyRequirements();
        services.AddPolicyUpdateEvents();
    }

    private static void AddPolicyUpdateEvents(this IServiceCollection services)
    {
        services.AddScoped<IPolicyUpdateEvent, RequireSsoPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, TwoFactorAuthenticationPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, SingleOrgPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, ResetPasswordPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, MaximumVaultTimeoutPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, FreeFamiliesForEnterprisePolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, OrganizationDataOwnershipPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, UriMatchDefaultPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, BlockClaimedDomainAccountCreationPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, AutomaticUserConfirmationPolicyEventHandler>();
        services.AddScoped<IPolicyUpdateEvent, DisableSendSyncPolicyEvent>();
        services.AddScoped<IPolicyUpdateEvent, SendOptionsSyncPolicyEvent>();
        services.AddScoped<IPolicyUpdateEvent, SendControlsSyncPolicyEvent>();
        services.AddScoped<IPolicyUpdateEvent, OrganizationUserNotificationPolicyEventHandler>();
    }

    private static void AddPolicyRequirements(this IServiceCollection services)
    {
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, DisableSendPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, SendOptionsPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, SendControlsPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, ResetPasswordPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, OrganizationDataOwnershipPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, RequireSsoPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, RequireTwoFactorPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, SingleOrganizationPolicyRequirementFactory>();
        services.AddScoped<IPolicyRequirementFactory<IPolicyRequirement>, AutomaticUserConfirmationPolicyRequirementFactory>();
    }
}
