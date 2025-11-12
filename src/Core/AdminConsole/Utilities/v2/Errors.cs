namespace Bit.Core.AdminConsole.Utilities.v2;

/// <summary>
/// A strongly typed error containing a reason that an action failed.
/// This is used for business logic validation and other expected errors, not exceptions.
/// </summary>
public abstract record Error(string Message);
/// <summary>
/// An <see cref="Error"/> type that maps to a NotFoundResult at the api layer.
/// </summary>
/// <param name="Message"></param>
public abstract record NotFoundError(string Message) : Error(Message);

public abstract record BadRequestError(string Message) : Error(Message);
public abstract record InternalError(string Message) : Error(Message);
