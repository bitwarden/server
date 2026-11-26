using Bit.Core.Pam.Services;
using Xunit;

namespace Bit.Core.Test.Pam.Services;

public class NoopLeasingBypassAuditTests
{
    [Fact]
    public async Task AuditCollectionChangeAsync_RunsTheChange()
    {
        var ran = false;

        await new NoopLeasingBypassAudit().AuditCollectionChangeAsync(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()],
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

        Assert.True(ran);
    }
}
