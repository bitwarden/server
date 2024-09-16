#nullable enable

using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.Utilities.Authorization;

/// <summary>
/// A command can implement this interface to calculate its authorization requirements and communicate those requirements
/// to the API layer.
/// </summary>
/// <typeparam name="T">The DTO type that is used as the argument for the main command method.</typeparam>
public interface IGetAuthorizationRequirements<T>
{
    /// <summary>
    /// Get the authorization requirements for the command.
    /// The result can be used to call <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationService"/>.AuthorizeAsync.
    /// This should be implemented for commands with complex or conditional authorization logic.
    /// </summary>
    /// <param name="request">The DTO object that is used as the argument for the main command method.</param>
    /// <returns>
    /// A sequence of tuples, where each tuple contains a resource affected by the command and the authorization requirement
    /// for that resource.
    /// </returns>
    public Task<IEnumerable<(object Resource, OperationAuthorizationRequirement Requirement)>> GetAuthorizationRequirements(T request);
}

