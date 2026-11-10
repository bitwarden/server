namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the MailCatcher container that captures outgoing SMTP mail and shows it in a web inbox.
/// </summary>
public static class MailCatcherResource
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<ContainerResource> ConfigureMailCatcher()
        {
            var (imageName, imageTag) = builder.GetImageParts("MailCatcher:Image");

            var smtpPort = builder.RequiredInt("MailCatcher:SmtpPort");
            var webPort = builder.RequiredInt("MailCatcher:WebPort");

            return builder
                .AddContainer("mailcatcher", imageName, imageTag)
                .WithLifetime(ContainerLifetime.Persistent)
                .WithEndpoint(port: smtpPort, name: "smtp", targetPort: 1025)
                .WithHttpEndpoint(port: webPort, name: "web", targetPort: webPort);
        }
    }
}
