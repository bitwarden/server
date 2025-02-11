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
        // Register policy requirement factories here
    }

    /// <summary>
    /// Used to register simple policy requirements where its factory method implements CreateRequirement.
    /// This MUST be used rather than calling AddScoped directly, because it will ensure the factory method has
    /// the correct type to be injected and then identified by <see cref="PolicyRequirementQuery"/> at runtime.
    /// </summary>
    /// <typeparam name="T">The specific PolicyRequirement being registered.</typeparam>
    private static void AddPolicyRequirement<T>(this IServiceCollection serviceCollection, CreateRequirement<T> factory)
        where T : class, IPolicyRequirement
        => serviceCollection.AddPolicyRequirement(_ => factory);

    /// <summary>
    /// Used to register policy requirements where you need to access additional dependencies (usually to return a
    /// curried factory method).
    /// This MUST be used rather than calling AddScoped directly, because it will ensure the factory method has
    /// the correct type to be injected and then identified by <see cref="PolicyRequirementQuery"/> at runtime.
    /// </summary>
    /// <typeparam name="T">The specific PolicyRequirement being registered.</typeparam>
    private static void AddPolicyRequirement<T>(this IServiceCollection serviceCollection,
        Func<IServiceProvider, CreateRequirement<T>> factory)
        where T : class, IPolicyRequirement
        => serviceCollection.AddScoped<CreateRequirement<IPolicyRequirement>>(factory);
}
