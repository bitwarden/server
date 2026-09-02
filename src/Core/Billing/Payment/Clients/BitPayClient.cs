using Bit.Core.Settings;
using BitPayLight;
using BitPayLight.Models.Invoice;

namespace Bit.Core.Billing.Payment.Clients;

public interface IBitPayClient
{
    Task<Invoice> GetInvoice(string invoiceId);
    Task<Invoice> CreateInvoice(Invoice invoice);
}

public class BitPayClient(
    GlobalSettings globalSettings) : IBitPayClient
{
    private readonly BitPay _bitPay = new(
        globalSettings.BitPay.Token, globalSettings.BitPay.Production ? Env.Prod : Env.Test);

    public Task<Invoice> GetInvoice(string invoiceId)
        => _bitPay.GetInvoice(invoiceId);

    public Task<Invoice> CreateInvoice(Invoice invoice)
        => _bitPay.CreateInvoice(invoice);
}
