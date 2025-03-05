using Bit.Core.Models.Commands;

namespace Bit.Api.Models.CommandResults;

public class BadRequestFailure<T> : FailureCommandResult<T>
{
    public BadRequestFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public BadRequestFailure(string errorMessage) : base(errorMessage)
    {
    }
}

public class BadRequestFailure : FailureCommandResult
{
    public BadRequestFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public BadRequestFailure(string errorMessage) : base(errorMessage)
    {
    }
}
