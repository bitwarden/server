using Bit.Core.Tools.ReportFeatures.Requests;

namespace Bit.Core.Tools.ReportFeatures.Interfaces;

public interface IDropPasswordHealthReportApplicationCommand
{
    Task DropPasswordHealthReportApplicationAsync(DropPasswordHealthReportApplicationRequest request);
}

