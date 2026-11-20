using Bit.Admin.Jobs;
using Bit.Core;
using Bit.DataMigrations;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace Admin.Test.Jobs;

public class DataMigrationsJobTests
{
    private readonly IDataMigrationRunner _runner = Substitute.For<IDataMigrationRunner>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly DataMigrationsJob _sut;

    public DataMigrationsJobTests()
    {
        _runner.Names.Returns(["First", "Second"]);
        _featureService.IsEnabled(FeatureFlagKeys.DataMigrations).Returns(true);
        _sut = new DataMigrationsJob(_runner, _featureService, Substitute.For<ILogger<DataMigrationsJob>>());
    }

    [Fact]
    public async Task Execute_FlagOff_DoesNothing()
    {
        _featureService.IsEnabled(FeatureFlagKeys.DataMigrations).Returns(false);

        await _sut.Execute(Substitute.For<IJobExecutionContext>());

        Assert.Empty(_runner.ReceivedCalls());
    }

    [Fact]
    public async Task Execute_RunsEveryMigrationLiveRespectingPauses()
    {
        await _sut.Execute(Substitute.For<IJobExecutionContext>());

        foreach (var name in new[] { "First", "Second" })
        {
            await _runner.Received(1).EnsurePartitionsAsync(name, Arg.Any<CancellationToken>());
            await _runner.Received(1).RunAsync(name,
                Arg.Is<DataMigrationRunOptions>(o => o.Scheduled && !o.DryRun && o.MaxBatches == null),
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Execute_OneMigrationThrows_OthersStillRun()
    {
        _runner.EnsurePartitionsAsync("First", Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException());

        await _sut.Execute(Substitute.For<IJobExecutionContext>());

        await _runner.Received(1).RunAsync("Second", Arg.Any<DataMigrationRunOptions>(), Arg.Any<CancellationToken>());
    }
}
