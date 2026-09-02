using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.SeederApi.Commands;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Commands;

/// <summary>Exercises <see cref="DestroySceneCommand"/> premium license-file cleanup against in-memory SQLite.</summary>
public sealed class DestroySceneCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly string _licenseDirectory;

    public DestroySceneCommandTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        // DatabaseContext.OnModelCreating resolves IDataProtectionProvider for the
        // User.Key / User.MasterPassword field converters, so DI must include it.
        services.AddDataProtection();
        services.AddDbContext<DatabaseContext>(opts => opts.UseSqlite(_connection));
        services.AddAutoMapper(typeof(UserRepository));

        _provider = services.BuildServiceProvider();
        _provider.GetRequiredService<DatabaseContext>().Database.EnsureCreated();

        _licenseDirectory = Path.Combine(Path.GetTempPath(), $"seeder-license-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_licenseDirectory, "user"));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_licenseDirectory))
        {
            Directory.Delete(_licenseDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task DestroyAsync_SelfHosted_DeletesSeededUserLicenseFile()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserWithPlayItemAsync(playId);
        var licenseFile = WriteLicenseFile(user.Id);

        await BuildCommand(selfHosted: true).DestroyAsync(playId);

        Assert.False(File.Exists(licenseFile));
        Assert.False(UserExists(user.Id));
    }

    [Fact]
    public async Task DestroyAsync_NotSelfHosted_StillDeletesSeededLicenseFile()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserWithPlayItemAsync(playId);
        var licenseFile = WriteLicenseFile(user.Id);

        await BuildCommand(selfHosted: false).DestroyAsync(playId);

        Assert.False(File.Exists(licenseFile));
        Assert.False(UserExists(user.Id));
    }

    [Fact]
    public async Task DestroyAsync_SelfHosted_MissingLicenseFile_StillSucceeds()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserWithPlayItemAsync(playId);

        await BuildCommand(selfHosted: true).DestroyAsync(playId);

        Assert.False(UserExists(user.Id));
    }

    [Fact]
    public async Task DestroyAsync_SelfHosted_UndeletableLicenseFile_DoesNotAbortDestroy()
    {
        var playId = Guid.NewGuid().ToString();
        var user = await SeedUserWithPlayItemAsync(playId);
        var licenseFile = WriteLicenseFile(user.Id);
        var userDirectory = Path.Combine(_licenseDirectory, "user");

        // The file genuinely exists, so File.Delete is reached and throws; the best-effort cleanup must
        // swallow it so the database teardown still succeeds. Windows blocks delete via an exclusive
        // handle; POSIX blocks it by removing write permission from the containing directory.
        FileStream? exclusiveHandle = null;
        if (OperatingSystem.IsWindows())
        {
            exclusiveHandle = new FileStream(licenseFile, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        else
        {
            File.SetUnixFileMode(userDirectory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        try
        {
            await BuildCommand(selfHosted: true).DestroyAsync(playId);

            Assert.False(UserExists(user.Id));
            Assert.True(File.Exists(licenseFile));
        }
        finally
        {
            exclusiveHandle?.Dispose();
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(userDirectory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    private DestroySceneCommand BuildCommand(bool selfHosted)
    {
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var mapper = _provider.GetRequiredService<IMapper>();
        var globalSettings = new GlobalSettings
        {
            SelfHosted = selfHosted,
            LicenseDirectory = _licenseDirectory,
        };

        return new DestroySceneCommand(
            _provider.GetRequiredService<DatabaseContext>(),
            _provider.GetRequiredService<ILogger<DestroySceneCommand>>(),
            new UserRepository(scopeFactory, mapper),
            new PlayItemRepository(scopeFactory, mapper),
            new ProviderRepository(scopeFactory, mapper),
            new OrganizationRepository(scopeFactory, mapper,
                _provider.GetRequiredService<ILogger<OrganizationRepository>>()),
            globalSettings);
    }

    private async Task<User> SeedUserWithPlayItemAsync(string playId)
    {
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
        var mapper = _provider.GetRequiredService<IMapper>();

        var user = new User
        {
            Id = CombGuid.Generate(),
            Email = $"destroy-{Guid.NewGuid():N}@bw.example",
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test-api-key",
        };
        await new UserRepository(scopeFactory, mapper).CreateAsync(user);

        var playItem = PlayItem.Create(user, playId);
        playItem.SetNewId();
        await new PlayItemRepository(scopeFactory, mapper).CreateAsync(playItem);

        return user;
    }

    private string WriteLicenseFile(Guid userId)
    {
        var path = Path.Combine(_licenseDirectory, "user", $"{userId}.json");
        File.WriteAllText(path, "{}");
        return path;
    }

    private bool UserExists(Guid userId) =>
        _provider.GetRequiredService<DatabaseContext>().Users.Any(u => u.Id == userId);
}
