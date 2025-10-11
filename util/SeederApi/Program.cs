using Bit.Seeder;
using Bit.SeederApi.Services;
using Bit.SharedWeb.Utilities;

var builder = WebApplication.CreateBuilder(args);

// Generate a new MangleId for a request
builder.Services.AddScoped<MangleId>(_ => new MangleId());

// Add services to the container.
builder.Services.AddControllers();

// Configure GlobalSettings from appsettings
var globalSettings = builder.Services.AddGlobalSettingsServices(builder.Configuration, builder.Environment);

// Common services
builder.Services.AddCustomDataProtectionServices(builder.Environment, globalSettings);
builder.Services.AddTokenizers();
builder.Services.AddDatabaseRepositories(globalSettings, forceEf: true);

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<Bit.Core.Entities.User>, Microsoft.AspNetCore.Identity.PasswordHasher<Bit.Core.Entities.User>>();

// Seeder services
builder.Services.AddSingleton<Bit.RustSDK.RustSdkService>();
builder.Services.AddScoped<Bit.Seeder.Factories.UserSeeder>();
builder.Services.AddScoped<IRecipeService, RecipeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRouting();

app.MapControllerRoute(
    name: "seed",
    pattern: "{controller=Seed}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "info",
    pattern: "{controller=Info}/{action=Index}/{id?}");

app.Run();
