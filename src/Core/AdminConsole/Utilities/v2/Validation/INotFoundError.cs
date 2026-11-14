namespace Bit.Core.AdminConsole.Utilities.v2.Validation;

/// <summary>
/// Marks an error as meaning the requested resource does not exist. The API layer uses this to choose the
/// response status, so Core errors stay free of HTTP concerns.
/// </summary>
public interface INotFoundError;
