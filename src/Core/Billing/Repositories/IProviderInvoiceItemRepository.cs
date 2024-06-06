using Bit.Core.Billing.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Repositories;

public interface IProviderInvoiceItemRepository : IRepository<ProviderInvoiceItem, Guid>
{
    Task<ProviderInvoiceItem> GetByInvoiceId(string invoiceId);
    Task<ICollection<ProviderInvoiceItem>> GetByProviderId(Guid providerId);
}
