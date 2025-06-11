using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.Logging;
using Quartz.Util;

namespace Bit.Core.AdminConsole.Utilities.DebuggingInstruments;

public class UserInviteDebuggingLogger : IUserInviteDebuggingLogger
{
    private readonly ILogger<UserInviteDebuggingLogger> _logger;

    public UserInviteDebuggingLogger(ILogger<UserInviteDebuggingLogger> logger)
    {
        _logger = logger;
    }

    public void Log(OrganizationUser allOrgUser)
    {
        this.Log([allOrgUser]);
    }

    public void Log(IEnumerable<OrganizationUser> allOrgUsers)
    {
        try
        {
            var invalidInviteState = allOrgUsers.Any(user => user.Status == OrganizationUserStatusType.Invited && user.Email.IsNullOrWhiteSpace());

            if (invalidInviteState)
            {
                _logger.LogWarning("Warning invalid invited users.");
            }

            var invalidNoneInviteState = allOrgUsers.Any(user => user.Status != OrganizationUserStatusType.Invited && !user.Email.IsNullOrWhiteSpace());

            if (invalidNoneInviteState)
            {
                _logger.LogWarning("Warning invalid invited users.");
            }
        }
        catch (Exception exception)
        {

            // Ensure that this debugging instrument does not interfere with the current flow.
            _logger.LogWarning(exception, "Unexpected exception from UserInviteDebuggingLogger");
        }
    }
}
