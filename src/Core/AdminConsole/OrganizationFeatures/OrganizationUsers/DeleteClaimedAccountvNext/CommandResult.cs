using OneOf;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccountvNext;

/// <summary>
/// Represents the result of a command.
/// </summary>
/// <param name="Id">
/// An ID associated with the request, used to identify results where there are multiple requests.
/// </param>
/// <param name="Result">
/// A <see cref="OneOf{Error, T}"/> type that contains an Error if the command execution failed, or the successful result of the command if it succeeded.
/// </param>
/// <typeparam name="T">The type of the successful result. If there is no successful result (void), use <see cref="CommandResult"/>.</typeparam>
public record CommandResult<T>(Guid Id, OneOf<Error, T> Result);

/// <summary>
/// Represents the result of a command where successful execution returns no value (void).
/// </summary>
/// <param name="Id">An ID associated with the request, used to identify results where there are multiple requests.</param>
/// <param name="Result">A <see cref="OneOf{Error, None}"/> type that contains an Error if the command execution failed.</param>
public record CommandResult(Guid Id, OneOf<Error, None> Result) : CommandResult<None>(Id, Result);

