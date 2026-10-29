using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Subscriptions.Organization.Commands;
using Bit.Subscriptions.Organization.Handlers;
using NSubstitute;
using Xunit;
using OrganizationEntity = Bit.Core.AdminConsole.Entities.Organization;

namespace Bit.Subscriptions.Organization.Test.Handlers;

public class RemovePrivilegedControlsHandlerTests
{
    private const string TrialMessage =
        "Work with your customer service representative to remove Privileged Controls.";

    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly FakeIsOnPrivilegedControlsSalesInitiatedTrialQuery _trialQuery = new();
    private readonly FakeRemovePrivilegedControlsCommand _command = new();
    private readonly RemovePrivilegedControlsHandler _sut;

    public RemovePrivilegedControlsHandlerTests() =>
        _sut = new RemovePrivilegedControlsHandler(_organizationRepository, _trialQuery, _command);

    [Fact]
    public async Task HandleAsync_OrganizationMissing_ThrowsNotFoundWithoutCheckingTrialOrCallingCommand()
    {
        var organizationId = Guid.NewGuid();
        _organizationRepository.GetByIdAsync(organizationId).Returns((OrganizationEntity?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(organizationId));

        Assert.Null(_trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_SalesInitiatedTrial_ThrowsBadRequestWithRepresentativeMessageWithoutCallingCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = true;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sut.HandleAsync(organization.Id));

        Assert.Equal(TrialMessage, exception.Message);
        Assert.Same(organization, _trialQuery.ReceivedOrganization);
        Assert.False(_command.Called);
    }

    [Fact]
    public async Task HandleAsync_NotOnTrial_PassesOrganizationToCommand()
    {
        var organization = ArrangeOrganization();
        _trialQuery.Result = false;

        await _sut.HandleAsync(organization.Id);

        Assert.True(_command.Called);
        Assert.Same(organization, _command.ReceivedOrganization);
    }

    private OrganizationEntity ArrangeOrganization()
    {
        var organization = new OrganizationEntity { Id = Guid.NewGuid() };
        _organizationRepository.GetByIdAsync(organization.Id).Returns(organization);
        return organization;
    }

    private sealed class FakeRemovePrivilegedControlsCommand : IRemovePrivilegedControlsCommand
    {
        public bool Called { get; private set; }
        public OrganizationEntity? ReceivedOrganization { get; private set; }

        public Task Run(OrganizationEntity organization)
        {
            Called = true;
            ReceivedOrganization = organization;
            return Task.CompletedTask;
        }
    }
}
