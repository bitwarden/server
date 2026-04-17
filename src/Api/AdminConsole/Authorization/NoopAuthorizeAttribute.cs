using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// A no-op attribute which documents an intentional choice to not use
/// <see cref="AuthorizeAttribute{T}"/> - for example, because you are manually handling
/// authorization in imperative code, or the endpoint does not require authorization.
/// Unlike <see cref="AllowAnonymousAttribute"/>, this does not bypass the class-level <see cref="AuthorizeAttribute"/>;
/// it indicates that no <b>additional</b> authorization is needed.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class NoopAuthorizeAttribute : Attribute;
