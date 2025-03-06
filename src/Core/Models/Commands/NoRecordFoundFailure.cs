namespace Bit.Core.Models.Commands;

public class NoRecordFoundFailure<T> : Failure<T>
{
    public NoRecordFoundFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public NoRecordFoundFailure(string errorMessage) : base(errorMessage)
    {
    }
}

public class NoRecordFoundFailure : Failure
{
    public NoRecordFoundFailure(IEnumerable<string> errorMessage) : base(errorMessage)
    {
    }

    public NoRecordFoundFailure(string errorMessage) : base(errorMessage)
    {
    }
}

