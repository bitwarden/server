using Bit.Core.Tools.Entities;

namespace Bit.Core;

public interface IGetPasswordHealthReportApplicationQuery
{
    Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplicationAsync(Guid organizationId);
}
