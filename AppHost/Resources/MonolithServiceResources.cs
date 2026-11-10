using Aspire.Hosting.Azure;
using Bit.AppHost.Configuration;

namespace Bit.AppHost.Resources;

/// <summary>
/// Adds the monolith services, wired to the database, secrets setup, mail and storage they depend on,
/// plus any extra projects listed under <c>AdditionalProjects</c> in configuration.
/// </summary>
internal static class MonolithServiceResources
{
    // Aspire names the endpoint it creates from each project's launch profile "http".
    internal const string HttpEndpointName = "http";

    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Configures and initializes the monolith services required for the distributed
        /// application, keyed by service.
        /// </summary>
        /// <param name="db">The SQL Server database resource builder.</param>
        /// <param name="secretsSetup">The executable resource builder for configuring secrets.</param>
        /// <param name="mail">The container resource builder for setting up the mail service.</param>
        /// <param name="azurite">The Azure Storage resource builder used to configure Azurite storage services.</param>
        /// <returns>A dictionary of configured project resource builders keyed by service.</returns>
        internal IReadOnlyDictionary<MonolithService, IResourceBuilder<ProjectResource>> ConfigureMonolithServices(
            IResourceBuilder<SqlServerDatabaseResource> db,
            IResourceBuilder<ExecutableResource> secretsSetup,
            IResourceBuilder<ContainerResource> mail,
            IResourceBuilder<AzureStorageResource> azurite)
        {
            var services = Enum.GetValues<MonolithService>().ToDictionary(service => service, service => service switch
            {
                MonolithService.Admin => builder.AddMonolithService<Projects.Admin>(service, db, secretsSetup, mail, azurite),
                MonolithService.Api => builder.AddMonolithService<Projects.Api>(service, db, secretsSetup, mail, azurite),
                MonolithService.Billing => builder.AddMonolithService<Projects.Billing>(service, db, secretsSetup, mail, azurite),
                MonolithService.Events => builder.AddMonolithService<Projects.Events>(service, db, secretsSetup, mail, azurite),
                MonolithService.EventsProcessor => builder.AddMonolithService<Projects.EventsProcessor>(service, db, secretsSetup, mail, azurite),
                MonolithService.Icons => builder.AddMonolithService<Projects.Icons>(service, db, secretsSetup, mail, azurite),
                MonolithService.Identity => builder.AddMonolithService<Projects.Identity>(service, db, secretsSetup, mail, azurite),
                MonolithService.Notifications => builder.AddMonolithService<Projects.Notifications>(service, db, secretsSetup, mail, azurite),
                MonolithService.Scim => builder.AddMonolithService<Projects.Scim>(service, db, secretsSetup, mail, azurite),
                MonolithService.Sso => builder.AddMonolithService<Projects.Sso>(service, db, secretsSetup, mail, azurite)
            });
            builder.ConfigureAdditionalProjects(services);
            return services;
        }

        /// <summary>
        /// Returns the host port of a service's <c>http</c> endpoint: its configured base port, plus one in
        /// self-hosted mode.
        /// </summary>
        internal int GetMonolithServicePort(MonolithService service)
        {
            var settings = builder.Settings;
            var basePort = settings.Services[service].BasePort;
            return settings.SelfHost ? basePort + 1 : basePort;
        }

        /// <summary>
        /// Configures additional projects specified in the configuration under "AdditionalProjects".
        /// This allows for dynamic inclusion of projects without code changes, useful for testing or temporary additions.
        /// </summary>
        /// <param name="services">All registered services; each additional project's ReferencedBy list selects which ones receive a reference.</param>
        private void ConfigureAdditionalProjects(IReadOnlyDictionary<MonolithService, IResourceBuilder<ProjectResource>> services)
        {
            // Add via user-secrets: dotnet user-secrets set "AdditionalProjects:<name>:Path" "<path/to/Project.csproj>"
            foreach (var (name, additionalProject) in builder.Settings.AdditionalProjects)
            {
                if (string.IsNullOrWhiteSpace(additionalProject.Path))
                {
                    continue;
                }

                var project = builder.AddProject(name, additionalProject.Path);
                foreach (var (_, service) in services.Where(s => additionalProject.ReferencedBy.Contains(s.Key.Name)))
                {
                    service.WithReference(project);
                }
            }
        }

        /// <summary>
        /// Adds and configures a monolith service of the specified project type. This includes linking the service to
        /// necessary resources such as a database, secrets setup, and the mail and storage resources its catalog entry asks for.
        /// </summary>
        /// <param name="monolithService">The service being added.</param>
        /// <param name="db">The SQL Server database resource to link to the service.</param>
        /// <param name="secretsSetup">The executable resource responsible for secrets setup.</param>
        /// <param name="mail">The container resource representing the mail service, used when the service sends mail.</param>
        /// <param name="azurite">The Azure Storage resource, waited for when the service uses storage.</param>
        /// <typeparam name="TProject">The type of project implementing the <see cref="IProjectMetadata"/> interface.</typeparam>
        /// <returns>The configured resource builder for the monolith service's project resource.</returns>
        private IResourceBuilder<ProjectResource> AddMonolithService<TProject>(MonolithService monolithService,
            IResourceBuilder<SqlServerDatabaseResource> db, IResourceBuilder<ExecutableResource> secretsSetup,
            IResourceBuilder<ContainerResource> mail, IResourceBuilder<AzureStorageResource> azurite)
            where TProject : IProjectMetadata, new()
        {
            // launchSettings provide the ports for the services
            var service = builder.AddProject<TProject>(monolithService.Name)
                .WithEndpoint(HttpEndpointName, e => e.Port = builder.GetMonolithServicePort(monolithService))
                .WithReference(db)
                .WaitFor(db)
                .WaitForCompletion(secretsSetup);

            if (builder.Settings.SelfHost)
            {
                service.WithEnvironment("developSelfHosted", "true");
            }

            if (monolithService.SendsMail)
            {
                service.WithReference(mail.SmtpEndpoint);
            }

            if (monolithService.UsesStorage)
            {
                service.WaitFor(azurite);
            }

            return service;
        }
    }
}
