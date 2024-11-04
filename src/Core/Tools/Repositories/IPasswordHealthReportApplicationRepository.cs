using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;

namespace Bit.Core.AdminConsole.Repositories;

public interface IPasswordHealthReportApplicationRepository : IRepository<PasswordHealthReportApplication, Guid>
{
    Task<ICollection<PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId);
}
