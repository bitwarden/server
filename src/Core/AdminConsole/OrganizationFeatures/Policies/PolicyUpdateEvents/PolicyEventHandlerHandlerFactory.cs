
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;
using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents;

public class PolicyEventHandlerHandlerFactory(
    IEnumerable<IPolicyUpdateEvent> allEventHandlers) : IPolicyEventHandlerFactory
{
    public OneOf<T, None> GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent
    {
        var tEventHandlers = allEventHandlers.OfType<T>().ToList();

        var matchingHandlers = tEventHandlers.Where(h => h.Type == policyType).ToList();

        if (matchingHandlers.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple {nameof(IPolicyUpdateEvent)} handlers of type {typeof(T).Name} found for {nameof(PolicyType)} {policyType}. " +
                $"Expected one {typeof(T).Name} handler per {nameof(PolicyType)}.");
        }

        var policyTEventHandler = matchingHandlers.SingleOrDefault();
        if (policyTEventHandler is null)
        {
            return new None();
        }

        return policyTEventHandler;
    }
}
