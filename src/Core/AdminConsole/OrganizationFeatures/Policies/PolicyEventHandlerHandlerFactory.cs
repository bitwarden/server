
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public class PolicyEventHandlerHandlerFactory(
    IEnumerable<IPolicyValidationEvent> validationHandlers,
    IEnumerable<IEnforceDependentPoliciesEvent> dependencyHandlers,
    IEnumerable<IOnPolicyPreUpsertEvent> preSaveHandlers,
    IEnumerable<IOnPolicyPostUpsertEvent> postSaveHandlers) : IPolicyEventHandlerFactory
{
    public T? GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent
    {
        var handlers = GetHandlerCollection<T>();

        return handlers.SingleOrDefault(h => h.Type == policyType);
    }

    private IEnumerable<T> GetHandlerCollection<T>() where T : IPolicyUpdateEvent
    {
        return typeof(T) switch
        {
            var t when t == typeof(IPolicyValidationEvent) => validationHandlers.Cast<T>(),
            var t when t == typeof(IEnforceDependentPoliciesEvent) => dependencyHandlers.Cast<T>(),
            var t when t == typeof(IOnPolicyPreUpsertEvent) => preSaveHandlers.Cast<T>(),
            var t when t == typeof(IOnPolicyPostUpsertEvent) => postSaveHandlers.Cast<T>(),
            _ => throw new ArgumentException($"Unsupported handler type: {typeof(T)}")
        };
    }
}


