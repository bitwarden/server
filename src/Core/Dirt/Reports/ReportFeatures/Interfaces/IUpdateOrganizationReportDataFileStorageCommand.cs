using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportDataFileStorageCommand
{
    Task<string> GetUploadUrlAsync(UpdateOrganizationReportDataRequest request, string reportFileId);
}
