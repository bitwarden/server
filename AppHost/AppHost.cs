using Bit.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
var secretsSetup = builder.ConfigureSecrets();
var db = builder.AddSqlServerDatabaseResource();
builder.ConfigureMigrations()
    .WaitFor(db)
    .ExcludeFromManifest()
    .WaitForCompletion(secretsSetup);
var azurite = builder.ConfigureAzurite();
var mail = builder.ConfigureMailCatcher();
var (_, api, billing, _, _) = builder.ConfigureServices(db, secretsSetup, mail, azurite);

#if ENABLE_NODEJS_COMMUNITY_PLUGIN
builder.ConfigureWebFrontend(api);
#endif

#if ENABLE_NGROK_COMMUNITY_PLUGIN
builder.ConfigureNgrok((billing, "http"));
#endif

builder.Build().Run();
