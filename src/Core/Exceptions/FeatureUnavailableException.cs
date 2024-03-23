namespace Bit.Core.Exceptions;

/// <summary>
/// Exception to throw when a requested feature is not yet enabled/available for the requesting context.
/// The client should know what features are available and should not call disabled features.
/// </summary>
public class FeatureUnavailableException : Exception
{
    public FeatureUnavailableException()
    { }

    public FeatureUnavailableException(string message)
        : base(message)
    { }
}
