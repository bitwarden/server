#nullable enable

using Bit.Core.Autofill.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Autofill.Repositories;

public interface IAutofillTriageReportRepository : IRepository<AutofillTriageReport, Guid>
{
    Task<IEnumerable<AutofillTriageReport>> GetActiveAsync(int skip, int take);
    Task ArchiveAsync(Guid id);
}
