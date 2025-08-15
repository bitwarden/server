using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IAddPasswordHealthReportApplicationCommand
{
    Task<PasswordHealthReportApplication> AddPasswordHealthReportApplicationAsync(AddPasswordHealthReportApplicationRequest request);
    Task<IEnumerable<PasswordHealthReportApplication>> AddPasswordHealthReportApplicationAsync(IEnumerable<AddPasswordHealthReportApplicationRequest> requests);
}
