using Bit.Core.Models.Commands;

namespace Bit.Api.Models.CommandResults;

public class NoRecordFoundFailure<T> : FailureCommandResult<T>
{
    public NoRecordFoundFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public NoRecordFoundFailure(string errorMessage) : base(errorMessage)
    {
    }
}

public class NoRecordFoundFailure : FailureCommandResult
{
    public NoRecordFoundFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public NoRecordFoundFailure(string errorMessage) : base(errorMessage)
    {
    }
}

