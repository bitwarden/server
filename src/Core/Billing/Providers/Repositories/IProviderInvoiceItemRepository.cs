using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Providers.Repositories;

public interface IProviderInvoiceItemRepository : IRepository<ProviderInvoiceItem, Guid>
{
    Task<ICollection<ProviderInvoiceItem>> GetByInvoiceId(string invoiceId);
    Task<ICollection<ProviderInvoiceItem>> GetByProviderId(Guid providerId);
}
