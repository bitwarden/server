namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the SimpleSAMLphp identity provider container used to test SSO; it starts only on request.
/// </summary>
public static class IdpResource
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<ContainerResource> ConfigureIdp()
        {
            var (imageName, imageTag) = builder.GetImageParts("Idp:Image");

            var port = builder.RequiredInt("Idp:Port");

            // This is hardcoded to localhost because the browser needs to navigate to the published SSO address,
            // not the internal Aspire endpoint reference (aspire.dev.internal). The port is still detected dynamically.
            var ssoBaseUrl = $"http://localhost:{builder.GetBitwardenServicePort("sso")}/saml2";
            var orgId = builder.AddParameter("sso-org-id")
                .WithDescription("Organization ID with SSO configured to use with the IdP service.");

            return builder
                .AddContainer("idp", imageName, imageTag)
                .WithHttpEndpoint(port: port, name: "http", targetPort: 8080)
                .WithEnvironment("SIMPLESAMLPHP_SP_ENTITY_ID",
                    ReferenceExpression.Create($"{ssoBaseUrl}/{orgId.Resource}"))
                .WithEnvironment("SIMPLESAMLPHP_SP_ASSERTION_CONSUMER_SERVICE",
                    ReferenceExpression.Create($"{ssoBaseUrl}/{orgId.Resource}/Acs"))
                .WithBindMount($"{builder.Required("WorkingDirectory")}/authsources.php",
                    "/var/www/simplesamlphp/config/authsources.php")
                .WithBindMount($"{builder.Required("WorkingDirectory")}/saml20-sp-remote.php",
                    "/var/www/simplesamlphp/metadata/saml20-sp-remote.php")
                .WithExplicitStart();
        }
    }
}
