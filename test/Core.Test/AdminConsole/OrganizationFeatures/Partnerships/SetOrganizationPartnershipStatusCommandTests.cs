using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;
using Bit.Core.AdminConsole.Repositories;
using Bit.Test.Common.AutoFixture;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Partnerships;

public class SetOrganizationPartnershipStatusCommandTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _earlier = _now.AddDays(-7);

    [Theory]
    [InlineData(PartnershipStatus.Active, PartnershipStatus.Inactive)]
    [InlineData(PartnershipStatus.Inactive, PartnershipStatus.Active)]
    public async Task SetStatusAsync_DifferentStatus_SavesNewStatus(PartnershipStatus current, PartnershipStatus target)
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider, current);

        var result = await sutProvider.Sut.SetStatusAsync(partnership.OrganizationId, target);

        Assert.Same(partnership, result.AsSuccess);
        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().Received(1)
            .ReplaceAsync(Arg.Is<OrganizationPartnership>(p => p.Id == partnership.Id && p.Status == target));
    }

    [Fact]
    public async Task SetStatusAsync_DifferentStatus_AdvancesRevisionDate()
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider, PartnershipStatus.Active);

        var result = await sutProvider.Sut.SetStatusAsync(partnership.OrganizationId, PartnershipStatus.Inactive);

        Assert.Equal(_now, result.AsSuccess.RevisionDate);
        Assert.Equal(_earlier, result.AsSuccess.CreationDate);
    }

    [Theory]
    [InlineData(PartnershipStatus.Active)]
    [InlineData(PartnershipStatus.Inactive)]
    public async Task SetStatusAsync_SameStatus_ReturnsPartnershipWithoutWriting(PartnershipStatus status)
    {
        var sutProvider = CreateSutProvider();
        var partnership = ArrangePartnership(sutProvider, status);

        var result = await sutProvider.Sut.SetStatusAsync(partnership.OrganizationId, status);

        Assert.Same(partnership, result.AsSuccess);
        Assert.Equal(_earlier, partnership.RevisionDate);
        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    [Fact]
    public async Task SetStatusAsync_NoPartnership_ReturnsNotFound()
    {
        var sutProvider = CreateSutProvider();

        var result = await sutProvider.Sut.SetStatusAsync(Guid.NewGuid(), PartnershipStatus.Inactive);

        Assert.Equal("not_found", Assert.IsType<PartnershipNotFound>(result.AsError).Code);
        await sutProvider.GetDependency<IOrganizationPartnershipRepository>().DidNotReceiveWithAnyArgs()
            .ReplaceAsync(default!);
    }

    private static SutProvider<SetOrganizationPartnershipStatusCommand> CreateSutProvider()
    {
        var sutProvider = new SutProvider<SetOrganizationPartnershipStatusCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static OrganizationPartnership ArrangePartnership(
        SutProvider<SetOrganizationPartnershipStatusCommand> sutProvider, PartnershipStatus status)
    {
        var partnership = new OrganizationPartnership
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Partner",
            Status = status,
            BindingMode = PartnershipBindingMode.Token,
            CreationDate = _earlier,
            RevisionDate = _earlier,
        };
        sutProvider.GetDependency<IOrganizationPartnershipRepository>()
            .GetByOrganizationIdAsync(partnership.OrganizationId)
            .Returns(partnership);
        return partnership;
    }
}
