

using Bit.Core.AdminConsole.Utilities.DebuggingInstruments;
using Bit.Core.Entities;

public class NoopUserInviteDebuggingLogger() : IUserInviteDebuggingLogger
{
    public void Log(OrganizationUser allOrgUser) { }
    public void Log(IEnumerable<OrganizationUser> allOrgUsers) { }
}
