using Bit.AgentFill.Entities;
using Bit.AgentFill.Notifiers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bit.AgentFill.Test.Notifiers;

public class LocalScriptAgentFillRequestNotifierTests : IDisposable
{
    private readonly string _directory = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;

    public void Dispose() => Directory.Delete(_directory, true);

    private string WriteScript(string body)
    {
        var path = Path.Combine(_directory, "script.sh");
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        return path;
    }

    [Fact]
    public async Task NotifyAsync_PassesApprovalIdAndUserIdToTheScript()
    {
        var output = Path.Combine(_directory, "args.txt");
        var sut = new LocalScriptAgentFillRequestNotifier(WriteScript($"echo \"$1 $2\" > '{output}'"),
            NullLogger<LocalScriptAgentFillRequestNotifier>.Instance);
        var request = new AgentFillApprovalRequest { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };

        await sut.NotifyAsync(request);

        Assert.Equal($"{request.Id} {request.UserId}", (await File.ReadAllTextAsync(output)).Trim());
    }

    [Fact]
    public async Task NotifyAsync_DoesNotThrow_WhenTheScriptFailsOrIsMissing()
    {
        var request = new AgentFillApprovalRequest { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        var failing = new LocalScriptAgentFillRequestNotifier(WriteScript("exit 1"),
            NullLogger<LocalScriptAgentFillRequestNotifier>.Instance);
        var missing = new LocalScriptAgentFillRequestNotifier(Path.Combine(_directory, "missing.sh"),
            NullLogger<LocalScriptAgentFillRequestNotifier>.Instance);

        await failing.NotifyAsync(request);
        await missing.NotifyAsync(request);
    }
}
