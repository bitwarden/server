#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Payment.Clients;
using Bit.Core.Entities;
using Bit.Core.Settings;
using BitPayLight.Models.Invoice;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Payment.Commands;

public interface ICreateBitPayInvoiceForCreditCommand
{
    Task<BillingCommandResult<string>> Run(
        ISubscriber subscriber,
        decimal amount,
        string redirectUrl);
}

public class CreateBitPayInvoiceForCreditCommand(
    IBitPayClient bitPayClient,
    GlobalSettings globalSettings,
    ILogger<CreateBitPayInvoiceForCreditCommand> logger) : BillingCommand<CreateBitPayInvoiceForCreditCommand>(logger), ICreateBitPayInvoiceForCreditCommand
{
    public Task<BillingCommandResult<string>> Run(
        ISubscriber subscriber,
        decimal amount,
        string redirectUrl) => HandleAsync<string>(async () =>
    {
        var (name, email, posData) = GetSubscriberInformation(subscriber);

        var invoice = new Invoice
        {
            Buyer = new Buyer { Email = email, Name = name },
            Currency = "USD",
            ExtendedNotifications = true,
            FullNotifications = true,
            ItemDesc = "Bitwarden",
            NotificationUrl = globalSettings.BitPay.NotificationUrl,
            PosData = posData,
            Price = Convert.ToDouble(amount),
            RedirectUrl = redirectUrl
        };

        var created = await bitPayClient.CreateInvoice(invoice);
        return created.Url;
    });

    private static (string? Name, string? Email, string POSData) GetSubscriberInformation(
        ISubscriber subscriber) => subscriber switch
        {
            User user => (user.Email, user.Email, $"userId:{user.Id},accountCredit:1"),
            Organization organization => (organization.Name, organization.BillingEmail,
                $"organizationId:{organization.Id},accountCredit:1"),
            Provider provider => (provider.Name, provider.BillingEmail, $"providerId:{provider.Id},accountCredit:1"),
            _ => throw new ArgumentOutOfRangeException(nameof(subscriber))
        };
}
