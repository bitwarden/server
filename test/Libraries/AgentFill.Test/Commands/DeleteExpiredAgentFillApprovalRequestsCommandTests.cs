using Bit.AgentFill.Commands;
using Bit.AgentFill.Repositories;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.AgentFill.Test.Commands;

public class DeleteExpiredAgentFillApprovalRequestsCommandTests
{
    [Fact]
    public async Task RunAsync_DeletesRequestsThatExpiredMoreThanADayAgo()
    {
        var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<IAgentFillApprovalRequestRepository>();
        repository.DeleteExpiredAsync(default).ReturnsForAnyArgs(7);
        var sut = new DeleteExpiredAgentFillApprovalRequestsCommand(repository, new FakeTimeProvider(now));

        var deleted = await sut.RunAsync();

        Assert.Equal(7, deleted);
        await repository.Received(1).DeleteExpiredAsync(now.UtcDateTime.AddDays(-1));
    }
}
