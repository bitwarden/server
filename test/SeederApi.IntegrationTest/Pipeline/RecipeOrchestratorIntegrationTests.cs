using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

/// <summary>
/// Verifies that BOTH <see cref="RecipeOrchestrator"/> <c>ExecuteAsync</c> overloads
/// (preset path + options path) actually invoke <c>EnsureOwnerEmailUnique</c>
/// against the real database. Regression protection against a future change that
/// silently removes the guard call from one overload.
/// </summary>
public sealed class RecipeOrchestratorIntegrationTests : IDisposable
{
    private const string _collidingEmail = "exists@bw.example";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly DatabaseContext _db;

    public RecipeOrchestratorIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // DatabaseContext.OnModelCreating resolves IDataProtectionProvider for the
        // User.Key / User.MasterPassword field converters, so DI must include it.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<DatabaseContext>(opts => opts.UseSqlite(_connection));

        _provider = services.BuildServiceProvider();
        _db = _provider.GetRequiredService<DatabaseContext>();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Execute_Preset_OwnerEmailCollidesWithExistingUser_ThrowsAsync()
    {
        SeedExistingUser(_collidingEmail);
        var orchestrator = NewOrchestrator(new NoOpManglerService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync(
                presetName: "any-preset-never-read",
                ownerEmailOverride: _collidingEmail));

        Assert.Contains(_collidingEmail, ex.Message);
        Assert.Contains("--mangle", ex.Message);
    }

    [Fact]
    public async Task Execute_Options_OwnerEmailCollidesWithExistingUser_ThrowsAsync()
    {
        SeedExistingUser(_collidingEmail);
        var orchestrator = NewOrchestrator(new NoOpManglerService());

        var options = new OrganizationVaultOptions
        {
            Name = "Test Org",
            Domain = "test.example",
            Users = 1,
            OwnerEmail = _collidingEmail,
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ExecuteAsync(options));

        Assert.Contains(_collidingEmail, ex.Message);
    }

    [Fact]
    public async Task Execute_Preset_ManglingEnabled_SkipsGuardEvenIfEmailExistsAsync()
    {
        // With --mangle, the per-run unique tag prevents collisions, so the guard
        // is skipped. We prove execution proceeded past the guard by using an
        // unknown preset name: failure comes from SeedReader ("not found"), not the
        // guard ("already exists"). Both throw InvalidOperationException, so we
        // discriminate by message content.
        SeedExistingUser(_collidingEmail);
        var orchestrator = NewOrchestrator(new ManglerService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync(
                presetName: "non-existent-preset",
                ownerEmailOverride: _collidingEmail));

        Assert.Contains("not found", ex.Message);
        Assert.DoesNotContain("already exists", ex.Message);
    }

    private void SeedExistingUser(string email)
    {
        _db.Users.Add(new EfUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test-api-key",
        });
        _db.SaveChanges();
    }

    private RecipeOrchestrator NewOrchestrator(IManglerService mangler)
    {
        // Mapper, LicensingService, and AttachmentStorageService are not exercised by the pre-flight
        // guard, which fires before BulkCommitter or any AutoMapper usage. Null-forgive them; if the
        // guard ever stops being the first thing in ExecuteAsync, these tests will fail loudly.
        var deps = new SeederDependencies(
            _db,
            null!,
            new PasswordHasher<User>(),
            mangler,
            null!,
            null!);
        return new RecipeOrchestrator(deps);
    }
}
