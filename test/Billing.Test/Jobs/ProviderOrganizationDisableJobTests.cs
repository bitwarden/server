using Bit.Billing.Jobs;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Xunit;

namespace Bit.Billing.Test.Jobs;

public class ProviderOrganizationDisableJobTests
{
    private readonly IProviderOrganizationRepository _providerOrganizationRepository;
    private readonly IOrganizationDisableCommand _organizationDisableCommand;
    private readonly ILogger<ProviderOrganizationDisableJob> _logger;
    private readonly ProviderOrganizationDisableJob _sut;

    public ProviderOrganizationDisableJobTests()
    {
        _providerOrganizationRepository = Substitute.For<IProviderOrganizationRepository>();
        _organizationDisableCommand = Substitute.For<IOrganizationDisableCommand>();
        _logger = Substitute.For<ILogger<ProviderOrganizationDisableJob>>();
        _sut = new ProviderOrganizationDisableJob(
            _providerOrganizationRepository,
            _organizationDisableCommand,
            _logger);
    }

    [Fact]
    public async Task Execute_NoOrganizations_LogsAndReturns()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var context = CreateJobExecutionContext(providerId, DateTime.UtcNow);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns((ICollection<ProviderOrganizationOrganizationDetails>)null);

        // Act
        await _sut.Execute(context);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs().DisableAsync(default, default);
    }

    [Fact]
    public async Task Execute_WithOrganizations_DisablesAllOrganizations()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(30);
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var org3Id = Guid.NewGuid();

        var organizations = new List<ProviderOrganizationOrganizationDetails>
        {
            new() { OrganizationId = org1Id },
            new() { OrganizationId = org2Id },
            new() { OrganizationId = org3Id }
        };

        var context = CreateJobExecutionContext(providerId, expirationDate);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns(organizations);

        // Act
        await _sut.Execute(context);

        // Assert
        await _organizationDisableCommand.Received(1).DisableAsync(org1Id, Arg.Any<DateTime?>());
        await _organizationDisableCommand.Received(1).DisableAsync(org2Id, Arg.Any<DateTime?>());
        await _organizationDisableCommand.Received(1).DisableAsync(org3Id, Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Execute_WithExpirationDate_PassesDateToDisableCommand()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var expirationDate = new DateTime(2025, 12, 31, 23, 59, 59);
        var orgId = Guid.NewGuid();

        var organizations = new List<ProviderOrganizationOrganizationDetails>
        {
            new() { OrganizationId = orgId }
        };

        var context = CreateJobExecutionContext(providerId, expirationDate);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns(organizations);

        // Act
        await _sut.Execute(context);

        // Assert
        await _organizationDisableCommand.Received(1).DisableAsync(orgId, expirationDate);
    }

    [Fact]
    public async Task Execute_WithNullExpirationDate_PassesNullToDisableCommand()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var organizations = new List<ProviderOrganizationOrganizationDetails>
        {
            new() { OrganizationId = orgId }
        };

        var context = CreateJobExecutionContext(providerId, null);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns(organizations);

        // Act
        await _sut.Execute(context);

        // Assert
        await _organizationDisableCommand.Received(1).DisableAsync(orgId, null);
    }

    [Fact]
    public async Task Execute_OneOrganizationFails_ContinuesProcessingOthers()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(30);
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var org3Id = Guid.NewGuid();

        var organizations = new List<ProviderOrganizationOrganizationDetails>
        {
            new() { OrganizationId = org1Id },
            new() { OrganizationId = org2Id },
            new() { OrganizationId = org3Id }
        };

        var context = CreateJobExecutionContext(providerId, expirationDate);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns(organizations);

        // Make org2 fail
        _organizationDisableCommand.DisableAsync(org2Id, Arg.Any<DateTime?>())
            .Throws(new Exception("Database error"));

        // Act
        await _sut.Execute(context);

        // Assert - all three should be attempted
        await _organizationDisableCommand.Received(1).DisableAsync(org1Id, Arg.Any<DateTime?>());
        await _organizationDisableCommand.Received(1).DisableAsync(org2Id, Arg.Any<DateTime?>());
        await _organizationDisableCommand.Received(1).DisableAsync(org3Id, Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Execute_ManyOrganizations_ProcessesWithLimitedConcurrency()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var expirationDate = DateTime.UtcNow.AddDays(30);

        // Create 20 organizations
        var organizations = Enumerable.Range(1, 20)
            .Select(_ => new ProviderOrganizationOrganizationDetails { OrganizationId = Guid.NewGuid() })
            .ToList();

        var context = CreateJobExecutionContext(providerId, expirationDate);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns(organizations);

        var concurrentCalls = 0;
        var maxConcurrentCalls = 0;
        var lockObj = new object();

        _organizationDisableCommand.DisableAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>())
            .Returns(callInfo =>
            {
                lock (lockObj)
                {
                    concurrentCalls++;
                    if (concurrentCalls > maxConcurrentCalls)
                    {
                        maxConcurrentCalls = concurrentCalls;
                    }
                }

                return Task.Delay(50).ContinueWith(_ =>
                {
                    lock (lockObj)
                    {
                        concurrentCalls--;
                    }
                });
            });

        // Act
        await _sut.Execute(context);

        // Assert
        Assert.True(maxConcurrentCalls <= 5, $"Expected max concurrency of 5, but got {maxConcurrentCalls}");
        await _organizationDisableCommand.Received(20).DisableAsync(Arg.Any<Guid>(), Arg.Any<DateTime?>());
    }

    [Fact]
    public async Task Execute_EmptyOrganizationsList_DoesNotCallDisableCommand()
    {
        // Arrange
        var providerId = Guid.NewGuid();
        var context = CreateJobExecutionContext(providerId, DateTime.UtcNow);
        _providerOrganizationRepository.GetManyDetailsByProviderAsync(providerId)
            .Returns(new List<ProviderOrganizationOrganizationDetails>());

        // Act
        await _sut.Execute(context);

        // Assert
        await _organizationDisableCommand.DidNotReceiveWithAnyArgs().DisableAsync(default, default);
    }

    private static IJobExecutionContext CreateJobExecutionContext(Guid providerId, DateTime? expirationDate)
    {
        var context = Substitute.For<IJobExecutionContext>();
        var jobDataMap = new JobDataMap
        {
            { "providerId", providerId.ToString() },
            { "expirationDate", expirationDate?.ToString("O") }
        };
        context.MergedJobDataMap.Returns(jobDataMap);
        return context;
    }
}
