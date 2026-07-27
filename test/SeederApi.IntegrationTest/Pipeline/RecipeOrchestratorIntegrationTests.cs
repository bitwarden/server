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
/// Verifies that BOTH <see cref="RecipeOrchestrator"/> <c>Execute</c> overloads
/// (preset path + options path) actually invoke <c>EnsureOwnerEmailUnique</c>
/// against the real database. Regression protection against a future change that
/// silently removes the guard call from one overload.
/// </summary>
public sealed class RecipeOrchestratorIntegrationTests : IDisposable
{
    private const string CollidingEmail = "exists@bw.example";

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
    public void Execute_Preset_OwnerEmailCollidesWithExistingUser_Throws()
    {
        SeedExistingUser(CollidingEmail);
        var orchestrator = NewOrchestrator(new NoOpManglerService());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            orchestrator.Execute(
                presetName: "any-preset-never-read",
                ownerEmailOverride: CollidingEmail));

        Assert.Contains(CollidingEmail, ex.Message);
        Assert.Contains("--mangle", ex.Message);
    }

    [Fact]
    public void Execute_Options_OwnerEmailCollidesWithExistingUser_Throws()
    {
        SeedExistingUser(CollidingEmail);
        var orchestrator = NewOrchestrator(new NoOpManglerService());

        var options = new OrganizationVaultOptions
        {
            Name = "Test Org",
            Domain = "test.example",
            Users = 1,
            OwnerEmail = CollidingEmail,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => orchestrator.Execute(options));

        Assert.Contains(CollidingEmail, ex.Message);
    }

    [Fact]
    public void Execute_Preset_ManglingEnabled_SkipsGuardEvenIfEmailExists()
    {
        // With --mangle, the per-run unique tag prevents collisions, so the guard
        // is skipped. We prove execution proceeded past the guard by using an
        // unknown preset name: failure comes from SeedReader ("not found"), not the
        // guard ("already exists"). Both throw InvalidOperationException, so we
        // discriminate by message content.
        SeedExistingUser(CollidingEmail);
        var orchestrator = NewOrchestrator(new ManglerService());

        var ex = Assert.Throws<InvalidOperationException>(() =>
            orchestrator.Execute(
                presetName: "non-existent-preset",
                ownerEmailOverride: CollidingEmail));

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
        // guard ever stops being the first thing in Execute, these tests will fail loudly.
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
