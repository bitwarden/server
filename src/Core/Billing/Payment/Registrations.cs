using Bit.Core.Billing.Payment.Clients;
using Bit.Core.Billing.Payment.Commands;
using Bit.Core.Billing.Payment.Queries;
using Bit.Core.Billing.Services;
using Bit.Core.Billing.Services.DiscountAudienceFilters;
using Bit.Core.Billing.Services.Implementations;
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

        // Discount services
        services.AddTransient<ISubscriptionDiscountService, SubscriptionDiscountService>();
        services.AddSingleton<IDiscountAudienceFilterFactory, DiscountAudienceFilterFactory>();

        // Queries
        services.AddTransient<IGetApplicableDiscountsQuery, GetApplicableDiscountsQuery>();
        services.AddTransient<IGetBillingAddressQuery, GetBillingAddressQuery>();
        services.AddTransient<IGetCreditQuery, GetCreditQuery>();
        services.AddTransient<IGetPaymentMethodQuery, GetPaymentMethodQuery>();
        services.AddTransient<IHasPaymentMethodQuery, HasPaymentMethodQuery>();
    }
}
