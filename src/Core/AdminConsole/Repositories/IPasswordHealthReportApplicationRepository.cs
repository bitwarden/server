using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IPasswordHealthReportApplicationRepository : IRepository<PasswordHealthReportApplication, Guid>
{
    Task<ICollection<PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId);
}
