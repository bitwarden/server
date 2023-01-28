using Bit.Core.Settings;

namespace Bit.Core.Utilities;

public class BitPayClient
{
    private readonly BitPayLight.BitPay _bpClient;

    public BitPayClient(GlobalSettings globalSettings)
    {
        if (CoreHelpers.SettingHasValue(globalSettings.BitPay.Token))
        {
            _bpClient = new BitPayLight.BitPay(globalSettings.BitPay.Token,
                globalSettings.BitPay.Production ? BitPayLight.Env.Prod : BitPayLight.Env.Test);
        }
    }

    public Task<BitPayLight.Models.Invoice.Invoice> GetInvoiceAsync(string id)
    {
        return _bpClient.GetInvoice(id);
    }

    public Task<BitPayLight.Models.Invoice.Invoice> CreateInvoiceAsync(BitPayLight.Models.Invoice.Invoice invoice)
    {
        return _bpClient.CreateInvoice(invoice);
    }
}
