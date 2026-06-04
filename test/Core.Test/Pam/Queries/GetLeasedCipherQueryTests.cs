using Bit.Core.Pam.Entities;
using Bit.Core.Pam.OrganizationFeatures.Queries;
using Bit.Core.Pam.Repositories;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

[SutProviderCustomize]
public class GetLeasedCipherQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_NoActiveLease_ReturnsNull(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ILeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns((Lease?)null);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Null(result);
        // Accessibility is never consulted when there is no lease.
        await sutProvider.GetDependency<ICipherRepository>()
            .DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ActiveLeaseButCipherNotAccessible_ReturnsNull(
        Guid userId, Guid cipherId, Lease lease)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ILeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns((CipherDetails?)null);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async Task GetLeasedCipherAsync_ActiveLeaseAndAccessible_ReturnsCipher(
        Guid userId, Guid cipherId, Lease lease)
    {
        var sutProvider = Setup();
        var cipher = new CipherDetails { Id = cipherId, Data = "2.iv|ct|mac" };
        sutProvider.GetDependency<ILeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(cipher);

        var result = await sutProvider.Sut.GetLeasedCipherAsync(userId, cipherId);

        Assert.NotNull(result);
        Assert.Equal(cipherId, result!.Id);
        Assert.Equal("2.iv|ct|mac", result.Data);
        // The active-lease lookup uses the TimeProvider's now.
        await sutProvider.GetDependency<ILeaseRepository>().Received(1)
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now);
    }

    private static SutProvider<GetLeasedCipherQuery> Setup()
    {
        var sutProvider = new SutProvider<GetLeasedCipherQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
