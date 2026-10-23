using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

/// <summary>
/// The result of one invite reminder run.
/// </summary>
/// <param name="Claimed">The number of members the run claimed.</param>
/// <param name="Failed">The number of claimed members whose reminder was not sent.</param>
/// <param name="SentMembers">The members whose reminder email was sent.</param>
public record SendInviteRemindersResult(
    int Claimed,
    int Failed,
    IReadOnlyCollection<OrganizationUserInviteReminder> SentMembers)
{
    public static SendInviteRemindersResult Empty { get; } = new(0, 0, []);

    public int Sent => SentMembers.Count;

    /// <summary>
    /// The number of members that got each reminder, by reminder number.
    /// </summary>
    public IReadOnlyDictionary<int, int> SentByReminderNumber =>
        SentMembers.GroupBy(m => (int)m.InviteRemindersSentCount).ToDictionary(g => g.Key, g => g.Count());
}
