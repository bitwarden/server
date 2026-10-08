using Bit.Admin.AgentFill.Jobs;
using Bit.AgentFill.Commands;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;

namespace Admin.Test.AgentFill;

public class DeleteExpiredAgentFillApprovalRequestsJobTests
{
    [Fact]
    public async Task Execute_RunsTheCleanupCommandOnce()
    {
        var command = Substitute.For<IDeleteExpiredAgentFillApprovalRequestsCommand>();
        command.RunAsync().Returns(3);
        var sut = new DeleteExpiredAgentFillApprovalRequestsJob(
            command, Substitute.For<ILogger<DeleteExpiredAgentFillApprovalRequestsJob>>());

        await sut.Execute(Substitute.For<IJobExecutionContext>());

        await command.Received(1).RunAsync();
    }
}
