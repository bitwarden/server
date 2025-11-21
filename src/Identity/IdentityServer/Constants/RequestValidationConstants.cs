namespace Bit.Identity.IdentityServer.RequestValidationConstants;

public static class CustomResponseConstants
{
    public static class ResponseKeys
    {
        /// <summary>
        /// Identifies the error model returned in the custom response when an error occurs.
        /// </summary>
        public static string ErrorModel => "ErrorModel";
        /// <summary>
        /// This Key is used when a user is in a single organization that requires SSO authentication. The identifier
        /// is used by the client to speed the redirection to the correct IdP for the user's organization.
        /// </summary>
        public static string SsoOrganizationIdentifier => "SsoOrganizationIdentifier";
    }
}

public static class SsoConstants
{
    /// <summary>
    /// These are messages and errors we return when SSO Validation is unsuccessful
    /// </summary>
    public static class RequestErrors
    {
        public static string SsoRequired => "sso_required";
        public static string SsoRequiredDescription => "Sso authentication is required.";
        public static string SsoTwoFactorRecoveryDescription => "Two-factor recovery has been performed. SSO authentication is required.";
    }
}
