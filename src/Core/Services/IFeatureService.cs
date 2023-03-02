namespace Bit.Core.Services;

public interface IFeatureService
{
    /// <summary>
    /// Checks whether online access to feature status is available.
    /// </summary>
    /// <returns>True if the service is online, otherwise false.</returns>
    bool IsOnline();
}
