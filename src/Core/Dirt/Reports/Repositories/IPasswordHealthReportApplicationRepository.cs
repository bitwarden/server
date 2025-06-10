using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IPasswordHealthReportApplicationRepository : IRepository<PasswordHealthReportApplication, Guid>
{
    Task<ICollection<PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId);
}
