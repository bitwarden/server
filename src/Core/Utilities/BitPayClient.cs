using Bit.Core.Settings;

namespace Bit.Core.Utilities;

#nullable enable

public class BitPayClient
{
    private readonly BitPayLight.BitPay? _bpClient;

    public BitPayClient(GlobalSettings globalSettings)
    {
        if (CoreHelpers.SettingHasValue(globalSettings.BitPay.Token))
        {
            _bpClient = new BitPayLight.BitPay(globalSettings.BitPay.Token,
                globalSettings.BitPay.Production ? BitPayLight.Env.Prod : BitPayLight.Env.Test);
        }
    }

    public async Task<BitPayLight.Models.Invoice.Invoice?> GetInvoiceAsync(string id)
    {
        if (_bpClient is null)
        {
            return null;
        }
        return await _bpClient.GetInvoice(id);
    }

    public async Task<BitPayLight.Models.Invoice.Invoice?> CreateInvoiceAsync(BitPayLight.Models.Invoice.Invoice invoice)
    {
        if (_bpClient is null)
        {
            return null;
        }
        return await _bpClient.CreateInvoice(invoice);
    }
}
