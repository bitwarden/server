using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Repositories;

public interface IPasswordHealthReportApplicationRepository : IRepository<PasswordHealthReportApplication, Guid>
{
    Task<ICollection<PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId);
}
