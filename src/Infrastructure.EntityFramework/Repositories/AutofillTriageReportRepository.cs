using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFAutofillTriageReport = Bit.Infrastructure.EntityFramework.Models.AutofillTriageReport;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class AutofillTriageReportRepository(IMapper mapper, IServiceScopeFactory serviceScopeFactory)
    : Repository<AutofillTriageReport, EFAutofillTriageReport, Guid>(
        serviceScopeFactory, mapper, context => context.AutofillTriageReports),
      IAutofillTriageReportRepository
{
    public async Task<IEnumerable<AutofillTriageReport>> GetActiveAsync(int skip, int take)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = GetDatabaseContext(scope);
        var results = await db.AutofillTriageReports
            .Where(r => !r.Archived)
            .OrderByDescending(r => r.CreationDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
        return Mapper.Map<List<AutofillTriageReport>>(results);
    }

    public async Task ArchiveAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var db = GetDatabaseContext(scope);
        await db.AutofillTriageReports
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Archived, true));
    }
}
