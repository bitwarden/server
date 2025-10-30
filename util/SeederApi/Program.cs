using Bit.Seeder;
using Bit.SeederApi.Extensions;
using Bit.SeederApi.Services;
using Bit.SharedWeb.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var globalSettings = builder.Services.AddGlobalSettingsServices(builder.Configuration, builder.Environment);

// Common services
builder.Services.AddCustomDataProtectionServices(builder.Environment, globalSettings);
builder.Services.AddTokenizers();
builder.Services.AddDatabaseRepositories(globalSettings, forceEf: true);

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Bit.Core.Entities.User>, Microsoft.AspNetCore.Identity.PasswordHasher<Bit.Core.Entities.User>>();

// Seeder services
builder.Services.AddSingleton<Bit.RustSDK.RustSdkService>();
builder.Services.AddScoped<Bit.Seeder.Factories.UserSeeder>();
builder.Services.AddScoped<ISceneService, SceneService>();
builder.Services.AddScoped<MangleId>(_ => new MangleId());
builder.Services.AddScenes();
builder.Services.AddQueries();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();

app.MapControllerRoute(name: "default", pattern: "{controller=Seed}/{action=Index}/{id?}");

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
