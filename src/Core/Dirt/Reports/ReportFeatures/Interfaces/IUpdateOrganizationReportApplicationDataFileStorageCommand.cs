using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportApplicationDataFileStorageCommand
{
    Task<string> GetUploadUrlAsync(UpdateOrganizationReportApplicationDataRequest request, string reportFileId);
}
