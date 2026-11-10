namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the persistent Redis container, with append-only persistence on a named volume.
/// </summary>
public static class RedisResource
{
    extension(IDistributedApplicationBuilder builder)
    {
        public IResourceBuilder<ContainerResource> ConfigureRedis()
        {
            var (imageName, imageTag) = builder.GetImageParts("Redis:Image");

            var port = builder.RequiredInt("Redis:Port");

            return builder
                .AddContainer("redis", imageName, imageTag)
                .WithLifetime(ContainerLifetime.Persistent)
                .WithEndpoint(port: port, name: "tcp", targetPort: 6379)
                .WithVolume("redis_data", "/data")
                .WithArgs("redis-server", "--appendonly", "yes");
        }
    }
}
