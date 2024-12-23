namespace Bit.Core.Enums;

public enum ErrorCode
{
    // Common errors (0000-0999)
    CommonError = 0000,
    CommonUserNotFound = 0001,
    CommonOrganizationNotFound = 0002,
    CommonUnauthorized = 0401,
    CommonInvalidToken = 0403,
    CommonResourceNotFound = 0404,
    CommonUnhandledError = 0500,

    // Identity errors (1000-1999)
    IdentityInvalidUsernameOrPassword = 1001,
    IdentitySsoRequired = 1002,
    IdentityEncryptionKeyMigrationRequired = 1003
}

public static class ErrorCodeExtensions
{
    public static string ToErrorCodeString(this ErrorCode code)
    {
        return ((int)code).ToString("D4"); // Formats to 4 digits with leading zeros
    }
}
