
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public class PolicyEventHandlerHandlerFactory(
    IEnumerable<IPolicyUpsertEvent> allEventHandlers) : IPolicyEventHandlerFactory
{

    public T? GetHandler<T>(PolicyType policyType) where T : IPolicyUpsertEvent
    {
        var tEventHandlers = allEventHandlers.OfType<T>();

        var policyTEventHandler = tEventHandlers.SingleOrDefault(h => h.Type == policyType);

        return policyTEventHandler;
    }

}


