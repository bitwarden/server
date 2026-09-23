using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.Dirt.Models;

/// <summary>
/// Reinterprets columns on events written before EventService correctly populated ActingUserId /
/// OrganizationUserId, so the response models can report those rows accurately without a data
/// migration. Each affected event type wrote a real value into the wrong column; a backfill would
/// move it to the right one, but reading it back with the same knowledge at response time has the
/// same effect for every consumer of these models and is safe to ship and roll back at any time.
/// </summary>
public static class EventLegacyFieldResolver
{
    /// <summary>
    /// Secret and project events written before the server populated ActingUserId recorded the
    /// human actor in UserId instead. Deliberately scoped to Secret_* and Project_*: on other
    /// types UserId is not the actor (Send access rows hold the owner while the accessor is
    /// unknown, system-user member events hold the member acted upon, and ServiceAccount_* rows
    /// hold an OrganizationUser id), so a blanket fallback would misattribute them.
    /// </summary>
    public static Guid? ResolveActingUserId(IEvent ev)
    {
        if (ev.ActingUserId.HasValue || !ev.UserId.HasValue)
        {
            return ev.ActingUserId;
        }

        var type = (int)ev.Type;
        var isSecretOrProjectEvent = type is >= 2100 and <= 2299;
        return isSecretOrProjectEvent ? ev.UserId : null;
    }

    /// <summary>
    /// ServiceAccount_UserAdded/Removed events written before the server populated
    /// OrganizationUserId recorded the granted member's OrganizationUser id in UserId instead.
    /// Scoped to exactly those two types: the sibling ServiceAccount_* events (Group/Created/
    /// Deleted) never wrote to UserId, so they must not pick up this fallback.
    /// </summary>
    public static Guid? ResolveOrganizationUserId(IEvent ev)
    {
        if (ev.OrganizationUserId.HasValue || !ev.UserId.HasValue)
        {
            return ev.OrganizationUserId;
        }

        return IsLegacyServiceAccountPeopleEvent(ev) ? ev.UserId : null;
    }

    /// <summary>
    /// The internal response model exposes UserId directly. On a legacy
    /// ServiceAccount_UserAdded/Removed row that value is an OrganizationUser id masquerading as a
    /// UserId (see <see cref="ResolveOrganizationUserId"/>), so it must not also be shown as-is
    /// under UserId once ResolveOrganizationUserId has claimed it.
    /// </summary>
    public static Guid? ResolveUserId(IEvent ev) =>
        IsLegacyServiceAccountPeopleEvent(ev) ? null : ev.UserId;

    private static bool IsLegacyServiceAccountPeopleEvent(IEvent ev) =>
        !ev.OrganizationUserId.HasValue &&
        ev.UserId.HasValue &&
        (ev.Type == EventType.ServiceAccount_UserAdded || ev.Type == EventType.ServiceAccount_UserRemoved);
}
