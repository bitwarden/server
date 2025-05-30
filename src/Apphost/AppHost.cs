var builder = DistributedApplication.CreateBuilder(args);

var bitwardenid = builder.AddParameter("bitwardenid", secret: true);
var bitwardenkey = builder.AddParameter("bitwardenkey", secret: true);

builder.AddGlobalSettings(c =>
{
    // Set selfhosted to true in all projects
    // and pass bitwarden id and keys
    c.WithEnvironment("globalSettings__selfHosted", "true")
     .WithEnvironment("globalSettings__installation__id", bitwardenid)
     .WithEnvironment("globalSettings__installation__key", bitwardenkey);
});

var iconServce = builder.AddProject<Projects.Icons>("icons", launchProfileName: "Icons");

var sql = builder.AddSqlServer("sql")
            .WithLifetime(ContainerLifetime.Persistent)
            .WithDataVolume("sql-data");
var theDb = sql.AddDatabase("the-db");

var identityapi = builder.AddProject<Projects.Identity>("identity", launchProfileName: "Identity")
    .WithHttpHealthCheck("/health")
    .WithDb(theDb);

builder.AddProject<Projects.Api>("api", launchProfileName: "Api")
    .WithHttpHealthCheck("/health")
    .WithReference(identityapi)
    .WithDb(theDb);

builder.AddProject<Projects.Billing>("billing", launchProfileName: "Billing")
    .WithUrlForEndpoint("http", url => url.Url = "/swagger")
    .WithHttpHealthCheck("/alive");

builder.Build().Run();
