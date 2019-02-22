using System;
using System.Threading.Tasks;

namespace Bit.Core.Utilities
{
    public class BitPayClient
    {
        private readonly NBitpayClient.Bitpay _bpClient;

        public BitPayClient(GlobalSettings globalSettings)
        {
            var btcSecret = new NBitcoin.BitcoinSecret(globalSettings.BitPay.Base58Secret,
                globalSettings.BitPay.Production ? null : NBitcoin.Network.TestNet);
            _bpClient = new NBitpayClient.Bitpay(btcSecret.PrivateKey,
                new Uri(globalSettings.BitPay.Production ? "https://bitpay.com/" : "https://test.bitpay.com/"));
        }

        public Task<bool> TestAccessAsync()
        {
            return _bpClient.TestAccessAsync(NBitpayClient.Facade.Merchant);
        }

        public Task<NBitpayClient.Invoice> GetInvoiceAsync(string id)
        {
            return _bpClient.GetInvoiceAsync(id);
        }

        public Task<NBitpayClient.Invoice> CreateInvoiceAsync(NBitpayClient.Invoice invoice)
        {
            return _bpClient.CreateInvoiceAsync(invoice);
        }
    }
}
