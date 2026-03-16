namespace Bit.Core.Auth.Models.Business.Tokenables;

public record TokenableValidationErrors
{
    public static TokenableValidationErrors InvalidToken => new("Invalid token.");

    public string ErrorMessage { get; }

    private TokenableValidationErrors(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }

    public static class ExpiringTokenables
    {
        // Used by clients to show better error message on token expiration, adjust both strings as-needed
        public static TokenableValidationErrors Expired => new("Expired token.");
    }
}
