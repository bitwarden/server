using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Vault.Services;
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
        services.AddAutoMapper(typeof(UserRepository));

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

    [Fact]
    public async Task Execute_Options_SelfHostedPremium_ResolvesLoggingAndRunsLicenseStepAsync()
    {
        // Regression guard for the DI wiring: the self-hosted individual path builds its own
        // ServiceCollection, and GenerateSelfHostUserLicenseStep resolves ILogger<T> from it. Without
        // AddLogging() in ExecuteAsync(IndividualUserOptions) this throws during step resolution before
        // the license step ever runs, so signerCalled stays false. Stubs stand in for the license
        // services so no certificate is required.
        var mapper = _provider.GetRequiredService<IMapper>();
        var signerCalled = false;
        var signer = new LicenseTestHelpers.StubSeederLicenseSigner(_ =>
        {
            signerCalled = true;
            return Task.FromResult(LicenseSigningResult.Skipped("no signing certificate configured"));
        });
        var licensing = new LicenseTestHelpers.StubLicensingService((_, _) => Task.CompletedTask);

        var deps = new SeederDependencies(
            _db, mapper, new PasswordHasher<User>(), new NoOpManglerService(), licensing,
            new NoopAttachmentStorageService(), signer);
        var orchestrator = new RecipeOrchestrator(deps);

        var options = new IndividualUserOptions
        {
            Email = $"selfhost-{Guid.NewGuid():N}@individual.example",
            SelfHosted = true,
            Premium = true,
        };

        // The pre-commit license step runs before BulkCommitter, whose LinqToDB bulk copy is not
        // reliable in this lightweight harness. Only the DI/logging wiring is under test here, so a
        // later commit-stage failure is out of scope; signerCalled proves the step materialized and ran.
        try
        {
            await orchestrator.ExecuteAsync(options);
        }
        catch
        {
            // A commit-stage failure is out of scope; signerCalled below proves the wiring under test.
            // If logging were unregistered, the step would never run and signerCalled would stay false.
        }

        Assert.True(signerCalled,
            "GenerateSelfHostUserLicenseStep did not run — the self-hosted individual path is missing logging registration.");
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
            null!,
            null!);
        return new RecipeOrchestrator(deps);
    }
}
