using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Core.AdminConsole.Models.Business.Provider;

public class ProviderUserInvite<T>
{
    public IEnumerable<T> UserIdentifiers { get; set; }
    public ProviderUserType Type { get; set; }
    public Guid InvitingUserId { get; set; }
    public Guid ProviderId { get; set; }
}

public static class ProviderUserInviteFactory
{
    public static ProviderUserInvite<string> CreateInitialInvite(
        IEnumerable<string> inviteeEmails,
        ProviderUserType type,
        Guid invitingUserId,
        Guid providerId
    )
    {
        return new ProviderUserInvite<string>
        {
            UserIdentifiers = inviteeEmails,
            Type = type,
            InvitingUserId = invitingUserId,
            ProviderId = providerId,
        };
    }

    public static ProviderUserInvite<Guid> CreateReinvite(
        IEnumerable<Guid> inviteeUserIds,
        Guid invitingUserId,
        Guid providerId
    )
    {
        return new ProviderUserInvite<Guid>
        {
            UserIdentifiers = inviteeUserIds,
            InvitingUserId = invitingUserId,
            ProviderId = providerId,
        };
    }
}
