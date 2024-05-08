using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Commands.Implementations;
using Bit.Core.Billing.Queries;
using Bit.Core.Billing.Queries.Implementations;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.Implementations;

namespace Bit.Core.Billing.Extensions;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddBillingOperations(this IServiceCollection services)
    {
        services.AddTransient<IOrganizationBillingService, OrganizationBillingService>();
        services.AddTransient<ISubscriberService, SubscriberService>();

        // Queries
        services.AddTransient<IProviderBillingQueries, ProviderBillingQueries>();

        // Commands
        services.AddTransient<IAssignSeatsToClientOrganizationCommand, AssignSeatsToClientOrganizationCommand>();
        services.AddTransient<ICancelSubscriptionCommand, CancelSubscriptionCommand>();
        services.AddTransient<ICreateCustomerCommand, CreateCustomerCommand>();
        services.AddTransient<IRemovePaymentMethodCommand, RemovePaymentMethodCommand>();
        services.AddTransient<IScaleSeatsCommand, ScaleSeatsCommand>();
        services.AddTransient<IStartSubscriptionCommand, StartSubscriptionCommand>();
    }
}
