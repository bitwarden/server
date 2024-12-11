using AutoMapper;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using EFProviderInvoiceItem = Bit.Infrastructure.EntityFramework.Billing.Models.ProviderInvoiceItem;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class ProviderInvoiceItemRepository(IMapper mapper, IServiceScopeFactory serviceScopeFactory)
    : Repository<ProviderInvoiceItem, EFProviderInvoiceItem, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.ProviderInvoiceItems
    ),
        IProviderInvoiceItemRepository
{
    public async Task<ICollection<ProviderInvoiceItem>> GetByInvoiceId(string invoiceId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from providerInvoiceItem in databaseContext.ProviderInvoiceItems
            where providerInvoiceItem.InvoiceId == invoiceId
            select providerInvoiceItem;

        return await query.ToArrayAsync();
    }

    public async Task<ICollection<ProviderInvoiceItem>> GetByProviderId(Guid providerId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from providerInvoiceItem in databaseContext.ProviderInvoiceItems
            where providerInvoiceItem.ProviderId == providerId
            select providerInvoiceItem;

        return await query.ToArrayAsync();
    }
}
