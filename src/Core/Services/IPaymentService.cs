using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Models.Business;
using Bit.Core.Enums;

namespace Bit.Core.Services
{
    public interface IPaymentService
    {
        Task CancelAndRecoverChargesAsync(ISubscriber subscriber);
        Task PurchasePremiumAsync(User user, PaymentMethodType paymentMethodType, string paymentToken,
            short additionalStorageGb);
        Task AdjustStorageAsync(IStorableSubscriber storableSubscriber, int additionalStorage, string storagePlanId);
        Task CancelSubscriptionAsync(ISubscriber subscriber, bool endOfPeriod = false);
        Task ReinstateSubscriptionAsync(ISubscriber subscriber);
        Task<bool> UpdatePaymentMethodAsync(ISubscriber subscriber, PaymentMethodType paymentMethodType,
            string paymentToken);
        Task<BillingInfo.BillingInvoice> GetUpcomingInvoiceAsync(ISubscriber subscriber);
        Task<BillingInfo> GetBillingAsync(ISubscriber subscriber);
    }
}
