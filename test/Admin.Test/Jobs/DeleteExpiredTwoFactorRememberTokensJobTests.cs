using Bit.Admin.Auth.Jobs;
using Bit.Core.Auth.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;

namespace Admin.Test.Jobs;

public class DeleteExpiredTwoFactorRememberTokensJobTests
{
    private readonly ITwoFactorRememberTokenRepository _repository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ILogger<DeleteExpiredTwoFactorRememberTokensJob> _logger;
    private readonly DeleteExpiredTwoFactorRememberTokensJob _sut;

    public DeleteExpiredTwoFactorRememberTokensJobTests()
    {
        _repository = Substitute.For<ITwoFactorRememberTokenRepository>();
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 22, 22, 0, 0, TimeSpan.Zero));
        _logger = Substitute.For<ILogger<DeleteExpiredTwoFactorRememberTokensJob>>();
        _sut = new DeleteExpiredTwoFactorRememberTokensJob(_repository, _timeProvider, _logger);
    }

    [Fact]
    public async Task Execute_Always_SweepsExpiredRows()
    {
        await _sut.Execute(CreateContext());

        await _repository.Received(1).DeleteExpiredAsync(Arg.Any<DateTime>());
    }

    /// <summary>
    /// The job owns the clock reading and passes it down, so the procedure and every Entity
    /// Framework implementation compare against the same instant.
    /// </summary>
    [Fact]
    public async Task Execute_Always_PassesItsOwnClockReading()
    {
        await _sut.Execute(CreateContext());

        await _repository.Received(1)
            .DeleteExpiredAsync(_timeProvider.GetUtcNow().UtcDateTime);
    }

    private static IJobExecutionContext CreateContext(CancellationToken cancellationToken = default)
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(cancellationToken);
        return context;
    }
}
