using Bit.AppHost;

var builder = DistributedApplication.CreateBuilder(args);
var secretsSetup = builder.ConfigureSecrets();
var isSelfHosted = builder.Configuration["globalSettings:selfHosted"]?.ToLowerInvariant() == "true";

// Add Pricing Service - use port from pricingUri in secrets
var pricingService =
    builder
        .AddProject("pricing-service",
            builder.Configuration["pricingServiceRelativePath"]
            ?? throw new ArgumentNullException("pricingServiceRelativePath", "Missing pricing service relative path"));

// Add Database and run migrations
var db = builder.AddSqlServerDatabaseResource(isSelfHosted);
builder.ConfigureMigrations(isSelfHosted)
    .WaitFor(db)
    .ExcludeFromManifest()
    .WaitForCompletion(secretsSetup);

var azurite = builder.ConfigureAzurite();

// Add MailCatcher
var mail = builder
    .AddContainer("mailcatcher", "sj26/mailcatcher:latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpoint(port: 10250, name: "smtp", targetPort: 1025) // SMTP port
    .WithHttpEndpoint(port: 1080, name: "web", targetPort: 1080);


// Add Services
builder.AddBitwardenService<Projects.Admin>(db, secretsSetup, mail, "admin");
var api = builder.AddBitwardenService<Projects.Api>(db, secretsSetup, mail, "api")
    .WithReference(pricingService)
    .WaitFor(azurite);
var billing = builder.AddBitwardenService<Projects.Billing>(db, secretsSetup, mail, "billing");
builder.AddBitwardenService<Projects.Identity>(db, secretsSetup, mail, "identity");
builder.AddBitwardenService<Projects.Notifications>(db, secretsSetup, mail, "notifications")
    .WaitFor(azurite);

// Add Client Apps
builder.AddBitwardenNpmApp("web-frontend", "web", api)
    .WithHttpsEndpoint(8080, 8080, "angular-http", isProxied: false)
    .WithUrl("https://bitwarden.test:8080")
    .WithExternalHttpEndpoints();
builder.AddBitwardenNpmApp("desktop-frontend", "desktop", api, "start");
builder.AddBitwardenNpmApp("browser-frontend", "browser", api, "build:bit:watch:chrome");

// Add Ngrok
builder.ConfigureNgrok((billing, "billing-http"));

builder.Build().Run();







