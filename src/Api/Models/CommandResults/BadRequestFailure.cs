using Bit.Core.Models.Commands;

namespace Bit.Api.Models.CommandResults;

public class BadRequestFailure<T> : Failure<T>
{
    public BadRequestFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public BadRequestFailure(string errorMessage) : base(errorMessage)
    {
    }
}

public class BadRequestFailure : Failure
{
    public BadRequestFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public BadRequestFailure(string errorMessage) : base(errorMessage)
    {
    }
}
