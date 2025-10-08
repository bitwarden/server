#nullable enable

using Bit.Core.AdminConsole.Enums;
using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyUpdateEvents.Interfaces;

public interface IPolicyEventHandlerFactory
{
    OneOf<T, None> GetHandler<T>(PolicyType policyType) where T : IPolicyUpdateEvent;
}
