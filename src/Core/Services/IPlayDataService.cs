using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;

namespace Bit.Core.Services;

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
