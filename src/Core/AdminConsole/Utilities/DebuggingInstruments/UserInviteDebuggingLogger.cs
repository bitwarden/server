using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;
using Quartz.Util;

namespace Bit.Core.AdminConsole.Utilities.DebuggingInstruments;

public class UserInviteDebuggingLogger(ILogger<UserInviteDebuggingLogger> logger) : IUserInviteDebuggingLogger
{
    public void Log(OrganizationUser allOrgUser)
    {
        Log([allOrgUser]);
    }

    public void Log(IEnumerable<OrganizationUser> allOrgUsers)
    {
        try
        {
            var invalidInviteState = allOrgUsers.Any(user => user.Status == OrganizationUserStatusType.Invited && user.Email.IsNullOrWhiteSpace());

            if (invalidInviteState)
            {
                logger.LogWarning("Warning invalid invited state.");
            }

            var invalidNoneInviteState = allOrgUsers.Any(user => user.Status != OrganizationUserStatusType.Invited && !user.Email.IsNullOrWhiteSpace());

            if (invalidNoneInviteState)
            {
                logger.LogWarning("Warning invalid non invited state.");
            }
        }
        catch (Exception exception)
        {

            // Ensure that this debugging instrument does not interfere with the current flow.
            logger.LogWarning(exception, "Unexpected exception from UserInviteDebuggingLogger");
        }
    }
}
