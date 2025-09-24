#nullable enable

using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyEventHandlerFactory
{
    T? GetHandler<T>(PolicyType policyType) where T : IPolicyUpsertEvent;
}
