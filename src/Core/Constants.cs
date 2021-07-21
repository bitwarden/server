namespace Bit.Core
{
    public static class Constants
    {
        public const int BypassFiltersEventId = 12482444;

        // File size limits - give 1 MB extra for cushion
        public const long FileSize101mb = 101L * 1024L * 1024L;
        public const long FileSize501mb = 501L * 1024L * 1024L;
    }

    public static class TokenPurposes 
    {
        public const string LinkSso = "LinkSso";
    }

    public static class AuthenticationSchemes
    {
        public const string BitwardenExternalCookieAuthenticationScheme = "bw.external";
    }
}
