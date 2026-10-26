namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

/// <summary>
/// An invited member whose reminder was claimed by
/// <see cref="Repositories.IOrganizationUserRepository.ClaimInviteReminderSequencesAsync"/>.
/// </summary>
public class OrganizationUserInviteReminder
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = null!;
    /// <summary>
    /// The reminder count after the claim. This is the number of the reminder to send.
    /// </summary>
    public byte InviteRemindersSentCount { get; set; }
}
