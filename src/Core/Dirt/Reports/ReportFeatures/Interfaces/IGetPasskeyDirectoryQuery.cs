using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetPasskeyDirectoryQuery
{
    Task<IEnumerable<PasskeyDirectoryEntry>> GetPasskeyDirectoryAsync();
}
