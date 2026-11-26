using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the SimpleSAMLphp identity provider container used to test SSO; it starts only on request.
/// </summary>
internal static class IdpResource
{
    private const string _endpointName = "http";

    private const int _containerPort = 8080;

    private const string _authSourcesPath = "/var/www/simplesamlphp/config/authsources.php";

    private const string _serviceProviderMetadataPath = "/var/www/simplesamlphp/metadata/saml20-sp-remote.php";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the identity provider container, pointed at the SSO service for the organization in the
        /// <c>sso-org-id</c> parameter.
        /// </summary>
        /// <returns>The resource builder for the identity provider container.</returns>
        internal IResourceBuilder<ContainerResource> ConfigureIdp()
        {
            var settings = builder.Settings;
            var (imageName, imageTag) = settings.Idp.GetImageParts();

            // This is hardcoded to localhost because the browser needs to navigate to the published SSO address,
            // not the internal Aspire endpoint reference (aspire.dev.internal). The port is still detected dynamically.
            var ssoBaseUrl = $"http://localhost:{builder.GetMonolithServicePort(MonolithService.Sso)}/saml2";
            var orgId = builder.AddParameter("sso-org-id")
                .WithDescription("Organization ID with SSO configured to use with the IdP service.");

            return builder
                .AddContainer("idp", imageName, imageTag)
                .WithHttpEndpoint(port: settings.Idp.Port, name: _endpointName, targetPort: _containerPort)
                .WithEnvironment("SIMPLESAMLPHP_SP_ENTITY_ID",
                    ReferenceExpression.Create($"{ssoBaseUrl}/{orgId.Resource}"))
                .WithEnvironment("SIMPLESAMLPHP_SP_ASSERTION_CONSUMER_SERVICE",
                    ReferenceExpression.Create($"{ssoBaseUrl}/{orgId.Resource}/Acs"))
                .WithBindMount($"{settings.WorkingDirectory}/authsources.php", _authSourcesPath)
                .WithBindMount($"{settings.WorkingDirectory}/saml20-sp-remote.php", _serviceProviderMetadataPath)
                .WithExplicitStart();
        }
    }
}
