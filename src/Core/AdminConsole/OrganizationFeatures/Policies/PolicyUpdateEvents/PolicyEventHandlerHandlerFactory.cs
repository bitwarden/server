
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
        var tEventHandlers = allEventHandlers.OfType<T>();

        var policyTEventHandler = tEventHandlers.SingleOrDefault(h => h.Type == policyType);
        if (policyTEventHandler is null)
        {
            return new None();
        }

        return policyTEventHandler;
    }
}
