using Bit.AppHost.Configuration;
using Bit.AppHost.Resources;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile(Path.Combine(builder.AppHostDirectory, "appsettings.local.json"), optional: true);
var secretsSetup = builder.ConfigureSecrets();
var databases = builder.ConfigureDatabases();
builder.ConfigureDatabaseMigrations(databases, secretsSetup);
var azurite = builder.ConfigureAzurite();
var mail = builder.ConfigureMailCatcher();
builder.ConfigureRedis();
builder.ConfigureIdp();
var services = builder.ConfigureMonolithServices(databases, secretsSetup, mail, azurite);

builder.ConfigureWebFrontend(services[MonolithService.Api]);

#if ENABLE_NGROK_COMMUNITY_PLUGIN
builder.ConfigureNgrok((services[MonolithService.Billing], MonolithServiceResources.HttpEndpointName));
#endif

builder.Build().Run();
