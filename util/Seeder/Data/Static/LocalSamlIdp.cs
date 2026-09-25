namespace Bit.Seeder.Data.Static;

/// <summary>
/// Fixed endpoints for the local dev SAML IdP shipped in <c>dev/docker-compose.yml</c>
/// (the <c>idp</c> profile, image <c>kenchan0130/simplesamlphp</c>). The signing certificate is
/// deliberately NOT stored here — it is fetched from the live metadata endpoint at seed time (see
/// <c>CreateSsoConfigStep</c>), so no certificate material lives in source and it always matches the
/// running image.
/// </summary>
internal static class LocalSamlIdp
{
    internal const string EntityId = "http://localhost:8090/simplesaml/saml2/idp/metadata.php";

    internal const string SingleSignOnServiceUrl = "http://localhost:8090/simplesaml/saml2/idp/SSOService.php";
}
