using Bit.AppHost.Configuration;
using Bit.AppHost.Configuration.Databases;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds a persistent container and vault database for each engine in <see cref="EngineMode.Container"/> mode;
/// External and Disabled engines get no resource.
/// </summary>
internal static class DatabaseResources
{
    private const string _databaseResourceSuffix = "-vault-dev";

    private const string _databaseName = "vault_dev";

    private const string _selfHostDatabaseName = "vault_dev_self_host";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Adds the containers and vault databases of the Container-mode engines, with their configured image,
        /// container name and data volume.
        /// </summary>
        /// <returns>The vault databases, <c>null</c> for each engine that is not in Container mode.</returns>
        internal DatabaseContainers ConfigureDatabases()
        {
            var settings = builder.Settings;
            var lineup = settings.Databases;
            var devSecrets = builder.LoadDevSecrets();
            return new DatabaseContainers
            {
                MsSql = lineup.MsSql.Mode == EngineMode.Container
                    ? builder.AddMsSqlDatabase(
                        lineup.MsSql, devSecrets.GetDatabasePassword(Engine.MsSql, settings.SelfHost), settings.SelfHost)
                    : null,
                Postgres = lineup.Postgres.Mode == EngineMode.Container
                    ? builder.AddPostgresDatabase(
                        lineup.Postgres, devSecrets.GetDatabasePassword(Engine.Postgres, settings.SelfHost))
                    : null,
                MySql = lineup.MySql.Mode == EngineMode.Container
                    ? builder.AddMySqlCompatibleDatabase(
                        Engine.MySql, lineup.MySql, devSecrets.GetDatabasePassword(Engine.MySql, settings.SelfHost))
                    : null,
                MariaDb = lineup.MariaDb.Mode == EngineMode.Container
                    ? builder.AddMySqlCompatibleDatabase(
                        Engine.MariaDb, lineup.MariaDb, devSecrets.GetDatabasePassword(Engine.MariaDb, settings.SelfHost))
                    : null
            };
        }

        private IResourceBuilder<SqlServerDatabaseResource> AddMsSqlDatabase(
            ServerEngineSettings msSql, string adminPassword, bool selfHost)
        {
            var engine = Engine.MsSql;
            var password = builder.AddPasswordParameter(engine, adminPassword);
            return builder
                .AddSqlServer(engine.Name, password, msSql.Port)
                .WithDatabaseContainerSettings(msSql)
                .WithDataVolume(VolumeNameOrDefault(msSql))
                .AddDatabase(DatabaseResourceName(engine), selfHost ? _selfHostDatabaseName : _databaseName);
        }

        private IResourceBuilder<PostgresDatabaseResource> AddPostgresDatabase(
            ServerEngineSettings postgres, string adminPassword)
        {
            var engine = Engine.Postgres;
            var password = builder.AddPasswordParameter(engine, adminPassword);
            // WithDataVolume picks the data directory from the image tag, so the image must be set first.
            return builder
                .AddPostgres(engine.Name, password: password, port: postgres.Port)
                .WithDatabaseContainerSettings(postgres)
                .WithDataVolume(VolumeNameOrDefault(postgres))
                .AddDatabase(DatabaseResourceName(engine), _databaseName);
        }

        private IResourceBuilder<MySqlDatabaseResource> AddMySqlCompatibleDatabase(
            Engine engine, ServerEngineSettings mySql, string adminPassword)
        {
            var password = builder.AddPasswordParameter(engine, adminPassword);
            return builder
                .AddMySql(engine.Name, password, mySql.Port)
                .WithDatabaseContainerSettings(mySql)
                .WithDataVolume(VolumeNameOrDefault(mySql))
                .AddDatabase(DatabaseResourceName(engine), _databaseName);
        }

        private IResourceBuilder<ParameterResource> AddPasswordParameter(Engine engine, string password) =>
            builder.AddParameter(engine.PasswordParameterName!, password, secret: true);
    }

    extension<TContainer>(IResourceBuilder<TContainer> container) where TContainer : ContainerResource
    {
        private IResourceBuilder<TContainer> WithDatabaseContainerSettings(ServerEngineSettings server)
        {
            var (imageName, imageTag) = server.GetImageParts();
            container
                .WithImage(imageName, imageTag)
                .WithLifetime(ContainerLifetime.Persistent);
            if (!string.IsNullOrWhiteSpace(server.ContainerName))
            {
                container.WithContainerName(server.ContainerName);
            }

            return container;
        }
    }

    private static string DatabaseResourceName(Engine engine) => engine.Name + _databaseResourceSuffix;

    private static string? VolumeNameOrDefault(ServerEngineSettings server) =>
        string.IsNullOrWhiteSpace(server.VolumeName) ? null : server.VolumeName;
}
