var builder = DistributedApplication.CreateBuilder(args);

var bitwardenid = builder.AddParameter("bitwardenid", secret: true);
var bitwardenkey = builder.AddParameter("bitwardenkey", secret: true);

// var mail = builder.AddMailPit("mail")
//             .WithLifetime(ContainerLifetime.Persistent)
//             .WithDataVolume("mail-data")
//             .WithHttpHealthCheck("/health");

builder.AddGlobalSettings(c =>
{
    // var smtp = mail.Resource.GetEndpoint("smtp");

    // Set selfhosted to true in all projects
    // and pass bitwarden id and keys
    c.WithEnvironment(context =>
    {
        var prefix = "globalSettings__";
        context.EnvironmentVariables[$"{prefix}selfHosted"] = "true";
        context.EnvironmentVariables[$"{prefix}installation__id"] = bitwardenid;
        context.EnvironmentVariables[$"{prefix}installation__key"] = bitwardenkey;

        // context.EnvironmentVariables[$"{prefix}mail__smtp__host"] = smtp.Property(EndpointProperty.Host);
        // context.EnvironmentVariables[$"{prefix}mail__smtp__port"] = smtp.Property(EndpointProperty.Port);
    });
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

builder.AddProject<Projects.Admin>("admin", launchProfileName: "Admin")
    .WithHttpHealthCheck("/alive")
    .WithReference(identityapi)
    .WithDb(theDb)
    .InstallAssets();

builder.Build().Run();
