using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.Utilities.DebuggingInstruments;

/// <summary>
/// Temporary code: Log warning when OrganizationUser is in an invalid state,
/// so we can identify which flow is causing the issue through Datadog.
/// </summary>
public interface IUserInviteDebuggingLogger
{
    void Log(OrganizationUser orgUser);
    void Log(IEnumerable<OrganizationUser> allOrgUsers);
}
