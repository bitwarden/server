namespace Bit.Core.Models.Common;

public class Result
{
    private Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; init; }

    public string[] Errors { get; init; }

    public static Result Success() => new(true, Array.Empty<string>());

    public static Result Failure(IEnumerable<string> errors) => new(false, errors);
}
