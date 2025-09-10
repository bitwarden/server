using Bit.Core.Billing.Payment.Clients;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Payment;

public static class Registrations
{
    public static void AddPaymentOperations(this IServiceCollection services)
    {
        // Commands
        services.AddTransient<IBitPayClient, BitPayClient>();
        services.AddTransient<ICreateBitPayInvoiceForCreditCommand, CreateBitPayInvoiceForCreditCommand>();
        services.AddTransient<IUpdateBillingAddressCommand, UpdateBillingAddressCommand>();
        services.AddTransient<IUpdatePaymentMethodCommand, UpdatePaymentMethodCommand>();

        // Queries
        services.AddTransient<IGetBillingAddressQuery, GetBillingAddressQuery>();
        services.AddTransient<IGetCreditQuery, GetCreditQuery>();
        services.AddTransient<IGetPaymentMethodQuery, GetPaymentMethodQuery>();
    }
}
