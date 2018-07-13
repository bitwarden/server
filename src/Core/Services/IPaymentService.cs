using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Models.Business;

namespace Bit.Core.Services
{
    public interface IPaymentService
    {
        Task CancelAndRecoverChargesAsync(ISubscriber subscriber);
        Task PurchasePremiumAsync(User user, string paymentToken, short additionalStorageGb);
        Task AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage, string storagePlanId);
        Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false);
        Task ReinstateSubscriptionAsync(ISubscriber subscriber);
        Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, string paymentToken);
        Task<BillingInfo.BillingInvoice> GetUpcomingInvoiceAsync(ISubscriber subscriber);
        Task<BillingInfo> GetBillingAsync(ISubscriber subscriber);
    }
}
