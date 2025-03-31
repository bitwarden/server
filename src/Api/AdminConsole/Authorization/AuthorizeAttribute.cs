#nullable enable

using Microsoft.AspNetCore.Authorization;

namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// An attribute which requires authorization using the specified requirement.
/// This uses the standard ASP.NET authorization middleware.
/// </summary>
/// <typeparam name="T">The IAuthorizationRequirement that will be used to authorize the user.</typeparam>
public class AuthorizeAttribute<T>
    : AuthorizeAttribute, IAuthorizationRequirementData
    where T : IAuthorizationRequirement, new()
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        var requirement = new T();
        return [requirement];
    }
}
