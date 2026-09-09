using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Provider = Bit.Core.AdminConsole.Entities.Provider.Provider;

namespace Bit.Core.Services;

/// <summary>
/// Service used to track added users, organizations, and providers during a Play session.
/// </summary>
public interface IPlayItemService
{
    /// <summary>
    /// Records a PlayItem entry for the given User created during a Play session.
    ///
    /// Does nothing if no Play Id is set for this http scope.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    Task Record(User user);
    /// <summary>
    /// Records a PlayItem entry for the given Organization created during a Play session.
    ///
    /// Does nothing if no Play Id is set for this http scope.
    /// </summary>
    /// <param name="organization"></param>
    /// <returns></returns>
    Task Record(Organization organization);
    /// <summary>
    /// Records a PlayItem entry for the given Provider created during a Play session.
    ///
    /// Does nothing if no Play Id is set for this http scope.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    Task Record(Provider provider);
}
