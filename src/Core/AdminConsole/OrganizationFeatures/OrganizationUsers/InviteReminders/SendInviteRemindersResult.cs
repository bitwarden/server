using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteReminders;

/// <summary>
/// What one invite reminder run did.
/// </summary>
/// <param name="Claimed">How many members the run claimed.</param>
/// <param name="Failed">How many claimed members didn't get their reminder.</param>
/// <param name="SentMembers">The members who got their reminder email.</param>
public record SendInviteRemindersResult(
    int Claimed,
    int Failed,
    IReadOnlyCollection<OrganizationUserInviteReminder> SentMembers)
{
    public static SendInviteRemindersResult Empty { get; } = new(0, 0, []);

    public int Sent => SentMembers.Count;

    /// <summary>
    /// How many members got each reminder, keyed by reminder number.
    /// </summary>
    public IReadOnlyDictionary<int, int> SentByReminderNumber =>
        SentMembers.GroupBy(m => (int)m.InviteRemindersSentCount).ToDictionary(g => g.Key, g => g.Count());
}
