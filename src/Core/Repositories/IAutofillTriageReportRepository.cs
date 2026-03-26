#nullable enable

using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface IAutofillTriageReportRepository : IRepository<AutofillTriageReport, Guid>
{
    Task<IEnumerable<AutofillTriageReport>> GetActiveAsync(int skip, int take);
    Task ArchiveAsync(Guid id);
}
