using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.ReportFeatures.Interfaces;

public interface IGetPasswordHealthReportApplicationQuery
{
    Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplicationAsync(Guid organizationId);
}
