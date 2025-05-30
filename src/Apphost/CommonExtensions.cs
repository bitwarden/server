public static class CommonExtensions
{
    public static IResourceBuilder<ProjectResource> WithDb(
        this IResourceBuilder<ProjectResource> builder, 
        IResourceBuilder<SqlServerDatabaseResource> db)
    {
           return builder.WithEnvironment("globalSettings__sqlServer__connectionString", db)
                         .WithEnvironment("globalSettings__databaseProvider", "sqlserver")
                         .WaitFor(db);
    }

    public static IDistributedApplicationBuilder AddGlobalSettings(
        this IDistributedApplicationBuilder builder, 
        Action<IResourceBuilder<IResourceWithEnvironment>> configure)
    {
        builder.Eventing.Subscribe<BeforeResourceStartedEvent>((e, ct) =>
        {
            if (e.Resource is ProjectResource p)
            {
                configure(builder.CreateResourceBuilder(p));
            }

            return Task.CompletedTask;
        });

        return builder;
    }
}
