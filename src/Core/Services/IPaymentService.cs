using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IPaymentService
    {
        Task PurchasePremiumAsync(User user, string paymentToken, short additionalStorageGb);
    }
}
