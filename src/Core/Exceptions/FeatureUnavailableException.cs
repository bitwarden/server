namespace Bit.Core.Exceptions;

/// <summary>
/// Exception to throw when a requested feature is not yet enabled/available for the requesting context.
/// </summary>
public class FeatureUnavailableException : NotFoundException
{
    public FeatureUnavailableException() { }

    public FeatureUnavailableException(string message)
        : base(message) { }
}
