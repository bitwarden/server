using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Commands.Implementations;

namespace Bit.Core.Billing.Extensions;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddBillingCommands(this IServiceCollection services)
    {
        services.AddSingleton<IRemovePaymentMethodCommand, RemovePaymentMethodCommand>();
    }
}
