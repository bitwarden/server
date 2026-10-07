using Bit.Admin.HostedServices;
using Bit.Core.Utilities;
using Bit.DataMigrations;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Admin.Test.HostedServices;

public class DatabaseMigrationHostedServiceTests
{
    private readonly IDbMigrator _dbMigrator = Substitute.For<IDbMigrator>();
    private readonly IDataMigrationRunner _runner = Substitute.For<IDataMigrationRunner>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartAsync_RunsDataMigrationsAtTheirMarkers(bool completed)
    {
        _runner.Names.Returns(["Known"]);
        _runner.RunToCompletionAsync("Known", Arg.Any<CancellationToken>()).Returns(completed);

        var onDataMigration = await StartAsync();

        Assert.Equal(completed, onDataMigration("Known"));
        Assert.False(onDataMigration("Unknown"));
        await _runner.DidNotReceive().RunToCompletionAsync("Unknown", Arg.Any<CancellationToken>());
    }

    private async Task<Func<string, bool>> StartAsync()
    {
        Func<string, bool>? onDataMigration = null;
        _dbMigrator.MigrateDatabase(true, Arg.Do<Func<string, bool>?>(h => onDataMigration = h), Arg.Any<CancellationToken>())
            .Returns(true);

        await new DatabaseMigrationHostedService(_dbMigrator, _runner, Substitute.For<ILogger<DatabaseMigrationHostedService>>())
            .StartAsync(CancellationToken.None);

        return onDataMigration!;
    }
}
