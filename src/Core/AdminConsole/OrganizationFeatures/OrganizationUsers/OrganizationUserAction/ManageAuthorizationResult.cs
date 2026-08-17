using Bit.Core.AdminConsole.Utilities.v2;
using OneOf;
using None = OneOf.Types.None;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction;

/// <summary>
/// The result of a single "can the acting user manage this target user" check performed by
/// <see cref="IOrganizationUserValidationService"/>: either the acting user is authorized, or they are denied
/// with an <see cref="Error"/> explaining why. Used instead of a bare nullable <see cref="Error"/> so a per-target
/// batch of results (see the bulk <c>CanManageAsync</c> overload) is self-describing rather than relying on the
/// convention that <c>null</c> means "allowed".
/// </summary>
public class ManageAuthorizationResult(OneOf<Error, None> result) : OneOfBase<Error, None>(result)
{
    public static readonly ManageAuthorizationResult Authorized = new(new None());

    public bool IsAuthorized => IsT1;
    public bool IsDenied => IsT0;
    public Error AsError => AsT0;

    public static implicit operator ManageAuthorizationResult(Error error) => new(error);

    /// <summary>
    /// Returns <c>true</c> and the denial reason when the acting user is not authorized; otherwise returns
    /// <c>false</c> with a <c>null</c> error.
    /// </summary>
    public bool TryGetError(out Error? error)
    {
        error = IsDenied ? AsError : null;
        return IsDenied;
    }
}
