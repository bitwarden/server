namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IExcludeCipherFromReportsCommand
{
    /// <summary>Excludes the user's personal login from their health reports. Requires premium.</summary>
    Task ExcludeAsync(Guid cipherId, Guid userId);
}
