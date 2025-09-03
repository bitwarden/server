namespace Bit.Core.AdminConsole.Utilities.Errors;

public record InsufficientPermissionsError<T>(string Message, T ErroredValue) : Error<T>(Message, ErroredValue)
{
    public const string Code = "Insufficient Permissions";

    public InsufficientPermissionsError(T ErroredValue) : this(Code, ErroredValue)
    {

    }
}
