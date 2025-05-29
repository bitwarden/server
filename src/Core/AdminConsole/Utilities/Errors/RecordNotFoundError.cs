namespace Bit.Core.AdminConsole.Utilities.Errors;

public record RecordNotFoundError<T>(string Message, T ErroredValue) : Error<T>(Message, ErroredValue)
{
    public const string Code = "Record Not Found";

    public RecordNotFoundError(T ErroredValue) : this(Code, ErroredValue)
    {

    }
}
