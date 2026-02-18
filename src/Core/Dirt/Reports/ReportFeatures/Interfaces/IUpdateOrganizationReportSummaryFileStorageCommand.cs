using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportSummaryFileStorageCommand
{
    Task<string> GetUploadUrlAsync(UpdateOrganizationReportSummaryRequest request, string reportFileId);
}
