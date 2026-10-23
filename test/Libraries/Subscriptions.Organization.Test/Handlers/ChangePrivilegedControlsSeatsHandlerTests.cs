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
            () => _sut.HandleAsync(organizationId, new PrivilegedControlsSeatsRequest { Seats = 15 }));

        Assert.Null(_trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrial_ThrowsBadRequestWithoutCallingCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { Seats = 15, MaxAutoscaleSeats = 20 }));

        Assert.Equal(TrialMessage, exception.Message);
        Assert.Same(organization, _trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_NotOnTrial_PassesOrganizationSeatsAndLimitToCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { Seats = 15, MaxAutoscaleSeats = 20 });

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

        await _sut.HandleAsync(organization.Id, new PrivilegedControlsSeatsRequest { Seats = 12 });

        Assert.True(_command.Called);
        Assert.Same(organization, _command.ReceivedOrganization);
        Assert.Equal(12, _command.ReceivedSeats);
        Assert.Null(_command.ReceivedMaxAutoscaleSeats);
    }

    private OrganizationEntity ArrangeOrganization()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
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
