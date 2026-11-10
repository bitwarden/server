using Bit.AppHost.Configuration;
using Bit.AppHost.Resources;

var builder = DistributedApplication.CreateBuilder(args);
var secretsSetup = builder.ConfigureSecrets();
var db = builder.AddSqlServerDatabaseResource();
builder.ConfigureDatabaseMigrations()
    .WaitFor(db)
    .ExcludeFromManifest()
    .WaitForCompletion(secretsSetup);
var azurite = builder.ConfigureAzurite();
var mail = builder.ConfigureMailCatcher();
builder.ConfigureRedis();
builder.ConfigureIdp();
var services = builder.ConfigureMonolithServices(db, secretsSetup, mail, azurite);

builder.ConfigureWebFrontend(services[MonolithService.Api]);

#if ENABLE_NGROK_COMMUNITY_PLUGIN
builder.ConfigureNgrok((services[MonolithService.Billing], MonolithServiceResources.HttpEndpointName));
#endif

builder.Build().Run();
