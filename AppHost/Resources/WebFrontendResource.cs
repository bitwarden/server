using Aspire.Hosting.JavaScript;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the web vault from the clients repository as an npm watch build that starts only on request.
/// </summary>
public static class WebFrontendResource
{
    extension(IDistributedApplicationBuilder builder)
    {
        public void ConfigureWebFrontend(IResourceBuilder<ProjectResource> api)
        {
            var basePort = builder.RequiredInt("WebFrontend:Port");
            var port = builder.IsSelfHosted() ? basePort + 1 : basePort;
            var scriptName = builder.IsSelfHosted() ? "build:bit:selfhost:watch" : "build:bit:watch";
            var url = builder.Required("WebFrontend:Url") + $":{port}";
            builder
                .AddBitwardenNpmApp("web-frontend", "web", api, port: port, scriptName: scriptName)
                .WithUrl(url)
                .WithExternalHttpEndpoints();
        }

        private IResourceBuilder<JavaScriptAppResource> AddBitwardenNpmApp(string name, string path,
            IResourceBuilder<ProjectResource> api, int port, string scriptName = "build:bit:watch")
        {
            return builder
                .AddJavaScriptApp(name, $"{builder.Required("ClientsPath")}/{path}", scriptName)
                .WithHttpsEndpoint(port, port, "angular-http", isProxied: false)
                .WithNpm(install: false)
                .WithReference(api)
                .WaitFor(api)
                .WithExplicitStart();
        }
    }
}
