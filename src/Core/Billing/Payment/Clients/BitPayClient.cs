using Bit.Core.Settings;
using BitPayLight;
using BitPayLight.Models.Invoice;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Payment.Clients;

public interface IBitPayClient
{
    Task<Invoice> GetInvoice(string invoiceId);
    Task<Invoice> CreateInvoice(Invoice invoice);
}

public class BitPayClient : IBitPayClient
{
    private readonly BitPay _bitPay;
    private readonly ILogger<BitPayClient> _logger;

    public BitPayClient(
        GlobalSettings globalSettings,
        ILogger<BitPayClient> logger)
    {
        TemporarilyLogBitPayConfiguration(globalSettings);

        _bitPay = new BitPay(
            globalSettings.BitPay.Token, globalSettings.BitPay.Production ? Env.Prod : Env.Test);
        _logger = logger;
    }

    public Task<Invoice> GetInvoice(string invoiceId)
        => _bitPay.GetInvoice(invoiceId);

    public Task<Invoice> CreateInvoice(Invoice invoice)
        => _bitPay.CreateInvoice(invoice);

    private void TemporarilyLogBitPayConfiguration(GlobalSettings globalSettings)
    {
        switch (globalSettings.BitPay)
        {
            case null:
                _logger.LogError("BitPay configuration is null");
                break;
            case { Token: null or "" }:
                _logger.LogError("BitPay token is null or empty");
                break;
            case { Token.Length: > 5 }:
                _logger.LogInformation("BitPay token: {Token}", globalSettings.BitPay.Token[..5]);
                break;
            default:
                _logger.LogInformation("BitPay token: {Token}", globalSettings.BitPay.Token);
                break;
        }

        _logger.LogInformation("BitPay production: {Production}", globalSettings.BitPay?.Production);
    }
}
