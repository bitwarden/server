using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Services;

/// <summary>
/// Service used to track added users and organizations during a Play session.
/// </summary>
public interface IPlayDataService
{
    /// <summary>
    /// Records a PlayData entry for the given User created during a Play session.
    ///
    /// Does nothing if no Play Id is set for this http scope.
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    Task Record(User user);
    /// <summary>
    /// Records a PlayData entry for the given Organization created during a Play session.
    ///
    /// Does nothing if no Play Id is set for this http scope.
    /// </summary>
    /// <param name="organization"></param>
    /// <returns></returns>
    Task Record(Organization organization);
}
