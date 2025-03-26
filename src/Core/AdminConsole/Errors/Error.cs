namespace Bit.Core.AdminConsole.Errors;

public record Error<T>(string Message, T ErroredValue);
