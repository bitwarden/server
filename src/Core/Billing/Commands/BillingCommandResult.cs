#nullable enable
using OneOf;

namespace Bit.Core.Billing.Commands;

public record BadRequest(string Response);
public record Conflict(string Response);
public record Unhandled(Exception? Exception = null, string Response = "Something went wrong with your request. Please contact support for assistance.");

public class BillingCommandResult<T> : OneOfBase<T, BadRequest, Conflict, Unhandled>
{
    private BillingCommandResult(OneOf<T, BadRequest, Conflict, Unhandled> input) : base(input) { }

    public static implicit operator BillingCommandResult<T>(T output) => new(output);
    public static implicit operator BillingCommandResult<T>(BadRequest badRequest) => new(badRequest);
    public static implicit operator BillingCommandResult<T>(Conflict conflict) => new(conflict);
    public static implicit operator BillingCommandResult<T>(Unhandled unhandled) => new(unhandled);
}
