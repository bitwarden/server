using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Queries;
using Bit.Core.Billing.Queries.Implementations;

namespace Bit.Core.Billing.Extensions;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddBillingCommands(this IServiceCollection services)
    {
        services.AddSingleton<ICancelSubscriptionCommand, CancelSubscriptionCommand>();
        services.AddSingleton<IRemovePaymentMethodCommand, RemovePaymentMethodCommand>();
    }

    public static void AddBillingQueries(this IServiceCollection services)
    {
        services.AddSingleton<IGetSubscriptionQuery, GetSubscriptionQuery>();
    }
}
