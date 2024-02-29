using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.EventsProcessor;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Bit.Core.Repositories.TableStorage;
using Bit.Core.Models.Data;
using NSubstitute;

namespace Bit.Core.Test.Repositories.TableStorage;

public class AzureTablesEventRepositoryTests
{

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    private readonly IEventService _eventService;
    private readonly AzureQueueEventProcessor _processor;
    private readonly AzureTablesEventRepository _repository;

    public AzureTablesEventRepositoryTests()
    {
        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _providerUserRepository = Substitute.For<IProviderUserRepository>();
        _applicationCacheService = Substitute.For<IApplicationCacheService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = new GlobalSettings();

        _globalSettings.Events.ConnectionString = "UseDevelopmentStorage=true";

        _eventService = new EventService(
            new AzureQueueEventWriteService(_globalSettings),
            _organizationUserRepository,
            _providerUserRepository,
            _applicationCacheService,
            _currentContext,
            _globalSettings
        );

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([ KeyValuePair.Create("azureStorageConnectionString", "UseDevelopmentStorage=true")])
            .Build();

        // TODO: Create logger with ITestOutputHelper
        _processor = new AzureQueueEventProcessor(NullLogger<AzureQueueEventProcessor>.Instance, configuration);

        _repository = new AzureTablesEventRepository("UseDevelopmentStorage=true");
    }

    private static readonly Guid _userId = Guid.Parse("97f72227-763e-4b81-8d88-5b9abffa2105");

    private static async Task LogUserEvent(IEventService eventService)
    {
        await eventService.LogUserEventAsync(_userId, EventType.User_LoggedIn);
    }

    private static async Task AssertUserEvent(IEventRepository eventRepository, DateTime startTime)
    {
        var eventsResult = await eventRepository.GetManyByUserAsync(_userId, startTime, DateTime.UtcNow, new PageOptions
        {

        });

        Assert.NotNull(eventsResult);
        var userEvent = Assert.Single(eventsResult.Data);
        Assert.Equal(DeviceType.EdgeBrowser, userEvent.DeviceType);
        Assert.Equal("1.1.1.1", userEvent.IpAddress);
        Assert.Equal(_userId, userEvent.UserId);
        Assert.Equal(_userId, userEvent.ActingUserId);
    }

    public static TheoryData<Func<IEventService, Task>, Func<IEventRepository, DateTime, Task>> TestData()
    {
        return new TheoryData<Func<IEventService, Task>, Func<IEventRepository, DateTime, Task>>
        {
            { LogUserEvent, AssertUserEvent }
        };
    }

    [Theory] // Skip if environment variable isn't there
    [MemberData(nameof(TestData))]
    [Trait("Environment", "Azure")]
    public async Task DoThings(Func<IEventService, Task> logEvent, Func<IEventRepository, DateTime, Task> asserter)
    {
        var startTime = DateTime.UtcNow;
        SetupContext("1.1.1.1", DeviceType.EdgeBrowser);

        await logEvent(_eventService);

        // Assert something
        var didProcess = await _processor.ProcessAsync(CancellationToken.None);
        Assert.True(didProcess);

        await asserter(_repository, startTime);
    }

    private void SetupContext(string ipAddress, DeviceType deviceType)
    {
        _currentContext.IpAddress = ipAddress;
        _currentContext.DeviceType = deviceType;
    }
}
