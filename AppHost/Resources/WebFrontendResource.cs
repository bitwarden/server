using Aspire.Hosting.JavaScript;
using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the web vault from the clients repository as an npm watch build that starts only on request.
/// </summary>
internal static class WebFrontendResource
{
    private const string _endpointName = "angular-http";

    private const string _watchScript = "build:bit:watch";

    private const string _selfHostWatchScript = "build:bit:selfhost:watch";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the web vault watch build, served over HTTPS on the configured port and pointed at the API.
        /// </summary>
        /// <param name="api">The API service the web vault calls.</param>
        internal void ConfigureWebFrontend(IResourceBuilder<ProjectResource> api)
        {
            var settings = builder.Settings;
            var basePort = settings.WebFrontend.Port;
            var port = settings.SelfHost ? basePort + 1 : basePort;
            var scriptName = settings.SelfHost ? _selfHostWatchScript : _watchScript;
            var url = settings.WebFrontend.Url + $":{port}";
            builder
                .AddBitwardenNpmApp("web-frontend", "web", api, port: port, scriptName: scriptName)
                .WithUrl(url)
                .WithExternalHttpEndpoints();
        }

        private IResourceBuilder<JavaScriptAppResource> AddBitwardenNpmApp(string name, string path,
            IResourceBuilder<ProjectResource> api, int port, string scriptName = _watchScript)
        {
            return builder
                .AddJavaScriptApp(name, $"{builder.Settings.ClientsPath}/{path}", scriptName)
                .WithHttpsEndpoint(port, port, _endpointName, isProxied: false)
                .WithNpm(install: false)
                .WithReference(api)
                .WaitFor(api)
                .WithExplicitStart();
        }
    }
}
