using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using Bit.Subscriptions.Organization.Models.Requests;
using Bit.Subscriptions.Organization.Queries;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class ChangePrivilegedControlsSeatsHandlerTests
{
    private const string TrialMessage =
        "Work with your customer service representative to change your Privileged Controls seats.";

    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery _trialQuery = new();
    private readonly FakeChangePrivilegedControlsSeatsCommand _command = new();
    private readonly ChangePrivilegedControlsSeatsHandler _sut;

    public ChangePrivilegedControlsSeatsHandlerTests() =>
        _sut = new ChangePrivilegedControlsSeatsHandler(_organizationRepository, _trialQuery, _command);

    [Fact]
    public async Task HandleAsync_OrganizationMissing_ThrowsNotFoundWithoutCheckingTrialOrCallingCommand()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.HandleAsync(organizationId, new PrivilegedControlsSeatsRequest { SeatAdjustment = 5 }));

        Assert.Null(_trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrial_ThrowsBadRequestWithoutCallingCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { SeatAdjustment = 5, MaxAutoscaleSeats = 20 }));

        Assert.Equal(TrialMessage, exception.Message);
        Assert.Same(organization, _trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_NotOnTrial_AddsAdjustmentToCurrentSeatsAndPassesLimitToCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { SeatAdjustment = 5, MaxAutoscaleSeats = 20 });

        Assert.True(_command.Called);
        Assert.Same(organization, _command.ReceivedOrganization);
        Assert.Equal(15, _command.ReceivedSeats);
        Assert.Equal(20, _command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task HandleAsync_NotOnTrialWithoutLimit_PassesNullLimitToCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { SeatAdjustment = 2 });

        Assert.True(_command.Called);
        Assert.Same(organization, _command.ReceivedOrganization);
        Assert.Equal(12, _command.ReceivedSeats);
        Assert.Null(_command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task HandleAsync_NegativeAdjustment_SubtractsFromCurrentSeats()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { SeatAdjustment = -3 });

        Assert.Equal(7, _command.ReceivedSeats);
    }

    [Fact]
    public async Task HandleAsync_OmittedAdjustment_KeepsCurrentSeatsAndPassesLimit()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { MaxAutoscaleSeats = 25 });

        Assert.Equal(10, _command.ReceivedSeats);
        Assert.Equal(25, _command.ReceivedMaxAutoscaleSeats);
    }

    [Fact]
    public async Task HandleAsync_OrganizationWithoutSeats_TreatsCurrentSeatsAsZero()
    {
        var organization = ArrangeOrganization(pamSeats: null);
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { SeatAdjustment = 5 });

        Assert.Equal(5, _command.ReceivedSeats);
    }

    private OrganizationEntity ArrangeOrganization(int? pamSeats = 10)
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid(), PamSeats = pamSeats };
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        return organization;
    }

    private sealed class FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery : IIsOnPrivilegedControlsSalesInitiatedTrialQuery
    {
        public bool Result { get; set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }

        public Task<bool> Run(OrganizationEntity organization)
        {
            ReceivedOrganization = organization;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeChangePrivilegedControlsSeatsCommand : IChangePrivilegedControlsSeatsCommand
    {
        public bool Called { get; private set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }
        public int ReceivedSeats { get; private set; }
        public int? ReceivedMaxAutoscaleSeats { get; private set; }

        public Task Run(OrganizationEntity organization, int seats, int? maxAutoscaleSeats)
        {
            Called = true;
            ReceivedOrganization = organization;
            ReceivedSeats = seats;
            ReceivedMaxAutoscaleSeats = maxAutoscaleSeats;
            return Task.CompletedTask;
        }
    }
}
