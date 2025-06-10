using Bit.Core.Dirt.Reports.Entities;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetPasswordHealthReportApplicationQuery
{
    Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplicationAsync(Guid organizationId);
}
