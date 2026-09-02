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
builder.ConfigureRedis();
builder.ConfigureIdp();
var services = builder.ConfigureServices(db, secretsSetup, mail, azurite);

builder.ConfigureWebFrontend(services["api"]);

#if ENABLE_NGROK_COMMUNITY_PLUGIN
builder.ConfigureNgrok((services["billing"], "http"));
#endif

builder.Build().Run();
