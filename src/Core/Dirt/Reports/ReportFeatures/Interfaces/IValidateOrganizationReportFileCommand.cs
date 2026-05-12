using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IValidateOrganizationReportFileCommand
{
    Task<bool> ValidateAsync(OrganizationReport report, string reportFileId);
}
