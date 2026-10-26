namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IExcludeCipherFromBrowserHealthCommand
{
    /// <summary>Excludes the user's personal login from browser health. Requires premium.</summary>
    Task ExcludeAsync(Guid cipherId, Guid userId);
}
