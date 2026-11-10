using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the persistent Redis container, with append-only persistence on a named volume.
/// </summary>
internal static class RedisResource
{
    private const string _endpointName = "tcp";

    private const int _containerPort = 6379;

    private const string _dataPath = "/data";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the persistent Redis container on the <c>redis_data</c> volume.
        /// </summary>
        /// <returns>The resource builder for the Redis container.</returns>
        internal IResourceBuilder<ContainerResource> ConfigureRedis()
        {
            var redis = builder.Settings.Redis;
            var (imageName, imageTag) = redis.GetImageParts();

            return builder
                .AddContainer("redis", imageName, imageTag)
                .WithLifetime(ContainerLifetime.Persistent)
                .WithEndpoint(port: redis.Port, name: _endpointName, targetPort: _containerPort)
                .WithVolume("redis_data", _dataPath)
                .WithArgs("redis-server", "--appendonly", "yes");
        }
    }
}
