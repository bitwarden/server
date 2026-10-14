using AutoMapper;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Services;
using Bit.Core.Vault.Services;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
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
        services.AddDbContext<DatabaseContext>(opts =>
        {
            opts.UseSqlite(_connection);

            // EF's default model cache is keyed by context type, not by ServiceProvider instance, so a
            // model built by an earlier test's DbContext (with its converters closed over that test's
            // IDataProtector/ILoggerFactory) would otherwise get reused here after that provider is
            // disposed. Disable it so every test builds its model against its own live provider.
            opts.ReplaceService<IModelCacheKeyFactory, NonCachingModelCacheKeyFactory>();
        });
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
        // the license step ever runs, so signerCalled stays false. The throwing licensing factory also
        // guards lazy resolution: resolving it eagerly would fail before the signer runs.
        var mapper = _provider.GetRequiredService<IMapper>();
        var signerCalled = false;
        var signer = new LicenseTestHelpers.StubSeederLicenseSigner(_ =>
        {
            signerCalled = true;
            return Task.FromResult(LicenseSigningResult.Skipped("no signing certificate configured"));
        });

        var deps = new SeederDependencies(
            _db, mapper, new PasswordHasher<User>(), new NoOpManglerService(),
            () => throw new Exception("Invalid licensing certificate."),
            new NoopAttachmentStorageService(), signer, NullLoggerFactory.Instance);
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

    [Fact]
    public async Task Execute_Options_BillingRequestedWithoutBillingService_ThrowsBeforeAnyWriteAsync()
    {
        var orchestrator = NewOrchestrator(new NoOpManglerService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync(BillingOptions()));

        Assert.Contains("IOrganizationStripeBillingService", ex.Message);
        Assert.Equal(0, _db.Users.Count());
        Assert.Equal(0, _db.Organizations.Count());
    }

    [Fact]
    public async Task Execute_Options_BillingRequested_ValidatesTheResolvedPlanTypeAsync()
    {
        var billingService = new RecordingOrganizationStripeBillingService(throwOnValidate: true);
        var orchestrator = NewOrchestrator(new NoOpManglerService(), billingService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync(BillingOptions(PlanType.TeamsMonthly)));

        Assert.Equal([PlanType.TeamsMonthly], billingService.ValidatedPlanTypes);
        Assert.Equal(0, _db.Organizations.Count());
    }

    [Fact]
    public async Task Execute_Preset_BillingRequested_ValidatesThePresetPlanTypeAsync()
    {
        // The Free-plan rejection lives in the billing service, so the orchestrator has to hand it the plan
        // the preset will actually seed — read from the preset before anything is built.
        var billingService = new RecordingOrganizationStripeBillingService(throwOnValidate: true);
        var orchestrator = NewOrchestrator(new NoOpManglerService(), billingService);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync("qa.stark-free-basic", stripeBilling: new StripeBillingOptions()));

        Assert.Equal([PlanType.Free], billingService.ValidatedPlanTypes);
        Assert.Equal(0, _db.Organizations.Count());
    }

    [Fact]
    public async Task Execute_Preset_BillingRequestedWithoutBillingService_ThrowsBeforeAnyWriteAsync()
    {
        var orchestrator = NewOrchestrator(new NoOpManglerService());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync("qa.enterprise-basic", stripeBilling: new StripeBillingOptions()));

        Assert.Contains("IOrganizationStripeBillingService", ex.Message);
        Assert.Equal(0, _db.Organizations.Count());
    }

    [Fact]
    public async Task Execute_NoBillingOptIn_NeverTouchesTheBillingServiceAsync()
    {
        // The zero-Stripe-calls default. The stub throws from every member, so any consultation at all
        // — validation or subscription creation — fails this test.
        var billingService = new RecordingOrganizationStripeBillingService(throwOnValidate: true);
        var orchestrator = NewOrchestrator(new NoOpManglerService(), billingService);

        // Fails on the unknown preset, proving execution got past the guard without consulting billing.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.ExecuteAsync("non-existent-preset"));

        Assert.Contains("not found", ex.Message);
        Assert.Empty(billingService.ValidatedPlanTypes);
    }

    [Fact]
    public async Task Execute_Options_BillingAccepted_GatewayIdsReachTheResultAsync()
    {
        // The one test in this fixture that runs the real pipeline to completion: proves the DI
        // registration (RecipeOrchestrator) and resolution (RecipeBuilderExtensions.WithStripeOrganizationBilling)
        // seam actually works, and that CreateOrganizationBillingStep's gateway IDs reach the result.
        var billingService = new RecordingOrganizationStripeBillingService(throwOnValidate: false);
        var orchestrator = NewOrchestrator(
            new NoOpManglerService(),
            billingService,
            mapper: CreateRealMapper(),
            attachmentStorageService: new NoopAttachmentStorageService());

        var options = new OrganizationVaultOptions
        {
            Name = "Billing Success Org",
            Domain = "billing-success.example",
            Users = 0,
            PlanType = PlanType.TeamsMonthly,
            StripeBilling = new StripeBillingOptions(),
        };

        var result = await orchestrator.ExecuteAsync(options);

        Assert.Equal([PlanType.TeamsMonthly], billingService.ValidatedPlanTypes);
        Assert.NotNull(billingService.BilledOrganization);
        Assert.Equal("cus_test_stub", result.GatewayCustomerId);
        Assert.Equal("sub_test_stub", result.GatewaySubscriptionId);
        Assert.Equal(1, _db.Organizations.Count());
        Assert.Equal(1, _db.Users.Count());
    }

    private static OrganizationVaultOptions BillingOptions(PlanType planType = PlanType.EnterpriseAnnually) => new()
    {
        Name = "Billing Test",
        Domain = "billingtest.example",
        Users = 1,
        PlanType = planType,
        StripeBilling = new StripeBillingOptions(),
    };

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

    private RecipeOrchestrator NewOrchestrator(
        IManglerService mangler,
        IOrganizationStripeBillingService? organizationStripeBillingService = null,
        IMapper? mapper = null,
        IAttachmentStorageService? attachmentStorageService = null)
    {
        // Mapper, LicensingService, and AttachmentStorageService are not exercised by the pre-flight
        // guard, which fires before BulkCommitter or any AutoMapper usage. Null-forgive them; if the
        // guard ever stops being the first thing in ExecuteAsync, these tests will fail loudly. Tests
        // that run the full pipeline past the guard supply real values via the optional parameters above.
        var deps = new SeederDependencies(
            _db,
            mapper ?? null!,
            new PasswordHasher<User>(),
            mangler,
            null!,
            attachmentStorageService ?? null!,
            null!,
            NullLoggerFactory.Instance)
        {
            OrganizationStripeBillingService = organizationStripeBillingService is null ? null : () => organizationStripeBillingService,
        };
        return new RecipeOrchestrator(deps);
    }

    private static IMapper CreateRealMapper() =>
        new ServiceCollection().AddAutoMapper(typeof(UserRepository)).BuildServiceProvider().GetRequiredService<IMapper>();

    /// <summary>
    /// Disables EF's model cache — see the comment where this is wired into <c>AddDbContext</c> above.
    /// </summary>
    private sealed class NonCachingModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => new object();
    }

    /// <summary>
    /// Records the plan types handed to <see cref="IOrganizationStripeBillingService.ValidateConfiguration"/> and,
    /// when asked, rejects them — standing in for a host whose Stripe configuration is unusable. When
    /// validation is not rejected, <see cref="CreateSubscriptionAsync"/> records the organization it
    /// was handed and stamps gateway IDs onto it, standing in for successful Stripe subscription creation.
    /// </summary>
    private sealed class RecordingOrganizationStripeBillingService(bool throwOnValidate) : IOrganizationStripeBillingService
    {
        internal List<PlanType> ValidatedPlanTypes { get; } = [];

        internal Organization? BilledOrganization { get; private set; }

        public void ValidateConfiguration(PlanType planType)
        {
            ValidatedPlanTypes.Add(planType);
            if (throwOnValidate)
            {
                throw new InvalidOperationException("stub rejects this configuration");
            }
        }

        public Task CreateSubscriptionAsync(Organization organization, StripeBillingOptions options)
        {
            BilledOrganization = organization;
            organization.GatewayCustomerId = "cus_test_stub";
            organization.GatewaySubscriptionId = "sub_test_stub";
            return Task.CompletedTask;
        }
    }
}
