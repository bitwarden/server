namespace Bit.Core.Auth.Models.Business.Tokenables;

public record TokenableValidationError
{
    public static TokenableValidationError InvalidToken => new("Invalid token.");

    public string ErrorMessage { get; }

    private TokenableValidationError(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    public static class ExpiringTokenables
    {
        // Used by clients to show better error message on token expiration, adjust both strings as-needed
        public static TokenableValidationError Expired => new("Expired token.");
    }
}
