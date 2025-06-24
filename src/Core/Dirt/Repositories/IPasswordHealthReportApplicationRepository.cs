using Bit.Core.Dirt.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IPasswordHealthReportApplicationRepository : IRepository<PasswordHealthReportApplication, Guid>
{
    Task<ICollection<PasswordHealthReportApplication>> GetByOrganizationIdAsync(Guid organizationId);
}
