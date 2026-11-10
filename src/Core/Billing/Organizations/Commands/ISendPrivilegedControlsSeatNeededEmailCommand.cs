using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Billing.Organizations.Commands;

/// <summary>
/// Tells an organization's Owners that a member joined without Privileged Controls access because the organization
/// reached its Privileged Controls seat limit.
/// </summary>
public interface ISendPrivilegedControlsSeatNeededEmailCommand
{
    /// <summary>
    /// Emails the organization's Owners naming the member. Call after the member is saved. Does nothing on
    /// self-hosted. A failure to send is logged, not thrown.
    /// </summary>
    Task RunAsync(Organization organization, string memberEmail);
}
