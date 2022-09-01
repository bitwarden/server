namespace Bit.Sso.Utilities;

/// <summary>
/// OpenID Connect Clients use scope values as defined in 3.3 of OAuth 2.0
/// [RFC6749]. These values represent the standard scope values supported
/// by OAuth 2.0 and therefore OIDC.
/// </summary>
/// <remarks>
/// See: https://openid.net/specs/openid-connect-basic-1_0.html#Scopes
/// </remarks>
public static class OpenIdConnectScopes
{
    /// <summary>
    /// REQUIRED. Informs the Authorization Server that the Client is making
    /// an OpenID Connect request. If the openid scope value is not present,
    /// the behavior is entirely unspecified.
    /// </summary>
    public const string OpenId = "openid";

    /// <summary>
    /// OPTIONAL. This scope value requests access to the End-User's default
    /// profile Claims, which are: name, family_name, given_name,
    /// middle_name, nickname, preferred_username, profile, picture,
    /// website, gender, birthdate, zoneinfo, locale, and updated_at.
    /// </summary>
    public const string Profile = "profile";

    /// <summary>
    /// OPTIONAL. This scope value requests access to the email and
    /// email_verified Claims.
    /// </summary>
    public const string Email = "email";

    /// <summary>
    /// OPTIONAL. This scope value requests access to the address Claim.
    /// </summary>
    public const string Address = "address";

    /// <summary>
    /// OPTIONAL. This scope value requests access to the phone_number and
    /// phone_number_verified Claims.
    /// </summary>
    public const string Phone = "phone";

    /// <summary>
    /// OPTIONAL. This scope value requests that an OAuth 2.0 Refresh Token
    /// be issued that can be used to obtain an Access Token that grants
    /// access to the End-User's UserInfo Endpoint even when the End-User is
    /// not present (not logged in).
    /// </summary>
    public const string OfflineAccess = "offline_access";

    /// <summary>
    /// OPTIONAL. Authentication Context Class Reference. String specifying
    /// an Authentication Context Class Reference value that identifies the
    /// Authentication Context Class that the authentication performed
    /// satisfied.
    /// </summary>
    /// <remarks>
    /// See: https://openid.net/specs/openid-connect-core-1_0.html#rfc.section.2
    /// </remarks>
    public const string Acr = "acr";
}
