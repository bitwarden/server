using Bit.Core.Pam.Services;
using Xunit;

namespace Bit.Core.Test.Pam.Services;

public class UnrestrictedCipherLeaseGateTests
{
    [Fact]
    public async Task GetOrganizationGatedCipherIdsAsync_GatesNothing()
    {
        var gated = await new UnrestrictedCipherLeaseGate().GetOrganizationGatedCipherIdsAsync(Guid.NewGuid(),
            [Guid.NewGuid()]);

        Assert.Empty(gated);
    }
}
