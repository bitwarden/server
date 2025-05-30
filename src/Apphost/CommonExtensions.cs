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

    public static IResourceBuilder<ProjectResource> InstallAssets(
        this IResourceBuilder<ProjectResource> builder)
    {
        var projectDirectory = Path.GetDirectoryName(builder.Resource.GetProjectMetadata().ProjectPath)!;

        var npmInstall = builder.ApplicationBuilder.AddExecutable(builder.Resource.Name + "-install-assets", 
            "npm", projectDirectory, "install");

        var npmBuild = builder.ApplicationBuilder.AddExecutable(builder.Resource.Name + "-build-assets", 
            "npm", projectDirectory, "run", "build")
            .WaitForCompletion(npmInstall);

        npmInstall.WithParentRelationship(builder);
        npmBuild.WithParentRelationship(npmInstall);
        
        return builder.WaitForCompletion(npmBuild);
    }
}
