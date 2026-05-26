using System.Linq.Expressions;
using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Enums;

public static class OrganizationUserStatusTypeExtensions
{
    /// <summary>
    /// In-memory mapping from <see cref="OrganizationUserStatusType"/> to its
    /// <see cref="OrganizationUserStatusTypeNew"/> equivalent.
    /// </summary>
    public static OrganizationUserStatusTypeNew ToOrganizationUserStatusTypeNew(this OrganizationUserStatusType status)
        => status switch
        {
            OrganizationUserStatusType.Invited => OrganizationUserStatusTypeNew.Invited,
            OrganizationUserStatusType.Accepted => OrganizationUserStatusTypeNew.Accepted,
            OrganizationUserStatusType.Confirmed => OrganizationUserStatusTypeNew.Confirmed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status,
                "Cannot convert this OrganizationUserStatusType value to OrganizationUserStatusTypeNew."),
        };

    /// <summary>
    /// Expression-tree equivalent of <see cref="ToOrganizationUserStatusTypeNew"/> for use inside
    /// EF Core <c>ExecuteUpdate</c> value-selectors and other LINQ-to-SQL trees, where method
    /// calls and switch expressions don't translate. Generic on the row type so callers (Core
    /// domain LINQ, EF model UPDATEs) can materialize it against whichever concrete entity they
    /// already have in hand — both share the <see cref="OrganizationUser.Status"/> property via
    /// inheritance. Both enums share the same <c>short</c> backing type, so this collapses to a
    /// single SQL <c>CAST</c> (often a no-op against a <c>SMALLINT</c> column).
    /// </summary>
    public static Expression<Func<T, OrganizationUserStatusTypeNew?>> OrganizationUserStatusToStatusNew<T>()
        where T : OrganizationUser
        => x => (OrganizationUserStatusTypeNew?)(short)x.Status;
}
