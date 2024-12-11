using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures.Requests;

namespace Bit.Core.Tools.ReportFeatures.Interfaces;

public interface IAddPasswordHealthReportApplicationCommand
{
    Task<PasswordHealthReportApplication> AddPasswordHealthReportApplicationAsync(
        AddPasswordHealthReportApplicationRequest request
    );
    Task<IEnumerable<PasswordHealthReportApplication>> AddPasswordHealthReportApplicationAsync(
        IEnumerable<AddPasswordHealthReportApplicationRequest> requests
    );
}
