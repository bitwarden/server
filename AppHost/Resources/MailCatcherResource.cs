using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the MailCatcher container that captures outgoing SMTP mail and shows it in a web inbox.
/// </summary>
internal static class MailCatcherResource
{
    private const string _smtpEndpointName = "smtp";

    private const string _webEndpointName = "web";

    private const int _smtpContainerPort = 1025;

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the persistent MailCatcher container with its SMTP and web inbox endpoints.
        /// </summary>
        /// <returns>The resource builder for the MailCatcher container.</returns>
        internal IResourceBuilder<ContainerResource> ConfigureMailCatcher()
        {
            var mailCatcher = builder.Settings.MailCatcher;
            var (imageName, imageTag) = mailCatcher.GetImageParts();

            return builder
                .AddContainer("mailcatcher", imageName, imageTag)
                .WithLifetime(ContainerLifetime.Persistent)
                .WithEndpoint(port: mailCatcher.SmtpPort, name: _smtpEndpointName, targetPort: _smtpContainerPort)
                .WithHttpEndpoint(port: mailCatcher.WebPort, name: _webEndpointName, targetPort: mailCatcher.WebPort);
        }
    }

    extension(IResourceBuilder<ContainerResource> mail)
    {
        /// <summary>
        /// The SMTP endpoint of a container added by <c>ConfigureMailCatcher</c>.
        /// </summary>
        internal EndpointReference SmtpEndpoint => mail.GetEndpoint(_smtpEndpointName);
    }
}
