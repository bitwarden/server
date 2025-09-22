#nullable enable
using OneOf;

namespace Bit.Core.Billing.Commands;

public record BadRequest(string Response);
public record Conflict(string Response);
public record Unhandled(Exception? Exception = null, string Response = "Something went wrong with your request. Please contact support for assistance.");

/// <summary>
/// A <see cref="OneOf"/> union type representing the result of a billing command.
/// <remarks>
/// Choices include:
/// <list type="bullet">
/// <item><description><typeparamref name="T"/>: Success</description></item>
/// <item><description><see cref="BadRequest"/>: Invalid input</description></item>
/// <item><description><see cref="Conflict"/>: A known, but unresolvable issue</description></item>
/// <item><description><see cref="Unhandled"/>: An unknown issue</description></item>
/// </list>
/// </remarks>
/// </summary>
/// <typeparam name="T">The successful result type of the operation.</typeparam>
public class BillingCommandResult<T> : OneOfBase<T, BadRequest, Conflict, Unhandled>
{
    private BillingCommandResult(OneOf<T, BadRequest, Conflict, Unhandled> input) : base(input) { }

    public static implicit operator BillingCommandResult<T>(T output) => new(output);
    public static implicit operator BillingCommandResult<T>(BadRequest badRequest) => new(badRequest);
    public static implicit operator BillingCommandResult<T>(Conflict conflict) => new(conflict);
    public static implicit operator BillingCommandResult<T>(Unhandled unhandled) => new(unhandled);

    public Task TapAsync(Func<T, Task> f) => Match(
        f,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask,
        _ => Task.CompletedTask);
}
