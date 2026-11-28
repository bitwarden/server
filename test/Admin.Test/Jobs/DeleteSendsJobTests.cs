using Bit.Admin.Auth.Jobs;
using Bit.Admin.Tools.Jobs;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;

namespace Admin.Test.Jobs;

public class DeleteSendsJobTests
{
    private readonly ISendRepository _sendRepository;
    private readonly INonAnonymousSendCommand _nonAnonymousSendCommand;
    private readonly DeleteSendsJob _sut;

    public DeleteSendsJobTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _nonAnonymousSendCommand = Substitute.For<INonAnonymousSendCommand>();

        var services = new ServiceCollection();
        services.AddSingleton(_nonAnonymousSendCommand);
        var serviceProvider = services.BuildServiceProvider();

        _sut = new DeleteSendsJob(
            _sendRepository,
            serviceProvider,
            Substitute.For<ILogger<DatabaseExpiredGrantsJob>>());
    }

    [Fact]
    public async Task Execute_DeletesExpiredSendsAndItemSendsWithDeletedCipher()
    {
        var expired = new Send { Id = Guid.NewGuid() };
        var orphaned = new Send { Id = Guid.NewGuid() };
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>()).Returns(new List<Send> { expired });
        _sendRepository.GetManyItemSendsWithDeletedCipherAsync().Returns(new List<Send> { orphaned });

        await _sut.Execute(Substitute.For<IJobExecutionContext>());

        await _nonAnonymousSendCommand.Received(1).DeleteSendAsync(expired);
        await _nonAnonymousSendCommand.Received(1).DeleteSendAsync(orphaned);
    }

    [Fact]
    public async Task Execute_SendInBothLists_DeletedOnce()
    {
        var send = new Send { Id = Guid.NewGuid() };
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>()).Returns(new List<Send> { send });
        _sendRepository.GetManyItemSendsWithDeletedCipherAsync().Returns(new List<Send> { send });

        await _sut.Execute(Substitute.For<IJobExecutionContext>());

        await _nonAnonymousSendCommand.Received(1).DeleteSendAsync(send);
    }

    [Fact]
    public async Task Execute_NothingToDelete_DeletesNothing()
    {
        _sendRepository.GetManyByDeletionDateAsync(Arg.Any<DateTime>()).Returns(new List<Send>());
        _sendRepository.GetManyItemSendsWithDeletedCipherAsync().Returns(new List<Send>());

        await _sut.Execute(Substitute.For<IJobExecutionContext>());

        await _nonAnonymousSendCommand.DidNotReceiveWithAnyArgs().DeleteSendAsync(default!);
    }
}
