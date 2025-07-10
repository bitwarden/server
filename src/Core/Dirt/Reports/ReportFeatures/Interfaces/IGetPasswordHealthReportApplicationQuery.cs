using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetPasswordHealthReportApplicationQuery
{
    Task<IEnumerable<PasswordHealthReportApplication>> GetPasswordHealthReportApplicationAsync(Guid organizationId);
}
