using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

/// <summary>
/// Receive Authorization service is responsible for checking if a Receive can be accessed by an uploader.
/// </summary>
public interface IReceiveAuthorizationService
{
    /// <summary>
    /// Checks if a <see cref="Receive" /> can be accessed while updating the <see cref="Receive" />, pushing a notification, and sending a reference event.
    /// </summary>
    /// <param name="receive"><see cref="Receive" /> used to determine access</param>
    /// <returns><see cref="bool" /> will be returned to determine if the user can access receive.
    /// </returns>
    bool Access(Receive receive);
    bool ReceiveCanBeAccessed(Receive receive);
}
