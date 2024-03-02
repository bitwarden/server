using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Repositories.TableStorage;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Bit.EventsProcessor;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;
using Xunit.Abstractions;

namespace Bit.Core.Test.Repositories.TableStorage;

public class AzureTablesEventRepositoryTests
{
    private const string EnvironmentVariableKey = "AZURE_CONNECTIONSTRING";


    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    private readonly IEventService _eventService;
    private readonly AzureQueueEventProcessor _processor;
    private readonly AzureTablesEventRepository _repository;

    public AzureTablesEventRepositoryTests(ITestOutputHelper testOutputHelper)
    {
        var logger = new XUnitTestLogger<AzureQueueEventProcessor>(testOutputHelper);

        _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        _providerUserRepository = Substitute.For<IProviderUserRepository>();
        _applicationCacheService = Substitute.For<IApplicationCacheService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _globalSettings = new GlobalSettings();

        var azureConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariableKey) ?? "UseDevelopmentStorage=true";

        _globalSettings.Events.ConnectionString = azureConnectionString;

        _eventService = new EventService(
            new AzureQueueEventWriteService(_globalSettings),
            _organizationUserRepository,
            _providerUserRepository,
            _applicationCacheService,
            _currentContext,
            _globalSettings
        );

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([KeyValuePair.Create("azureStorageConnectionString", azureConnectionString)])
            .Build();

        _processor = new AzureQueueEventProcessor(logger, configuration);

        _repository = new AzureTablesEventRepository(azureConnectionString);

        _applicationCacheService
            .GetOrganizationAbilitiesAsync()
            .Returns(new Dictionary<Guid, OrganizationAbility>
            {
                {
                    _org1Id,
                    new OrganizationAbility
                    {
                        Enabled = true,
                        UseEvents = true,
                    }
                },
                {
                    _org2Id,
                    new OrganizationAbility
                    {
                        Enabled = true,
                        UseEvents = true,
                    }
                },
                {
                    _org3Id,
                    new OrganizationAbility
                    {
                        Enabled = true,
                        UseEvents = true,
                    }
                }
            });

        var orgMembershipMap = new Dictionary<Guid, CurrentContextOrganization[]>
        {
            { _userInSingleOrg, [new CurrentContextOrganization { Id = _org1Id }]},
            {
                _userInTwoOrgs,
                [
                    new CurrentContextOrganization { Id = _org2Id },
                    new CurrentContextOrganization { Id = _org3Id },
                ]
            },
        };

        _currentContext
            .OrganizationMembershipAsync(Arg.Any<IOrganizationUserRepository>(), Arg.Any<Guid>())
            .Returns((CallInfo callInfo) =>
            {
                return Task.FromResult<ICollection<CurrentContextOrganization>>(
                    orgMembershipMap.TryGetValue(callInfo.Arg<Guid>(), out var memberships)
                        ? memberships
                        : []
                );
            });
    }

    private static readonly Guid _userInSingleOrg = Guid.NewGuid();
    private static readonly Guid _userInTwoOrgs = Guid.NewGuid();
    private static readonly Guid _org1Id = Guid.NewGuid();
    private static readonly Guid _org2Id = Guid.NewGuid();
    private static readonly Guid _org3Id = Guid.NewGuid();
    private static readonly Cipher _cipher = new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = _org1Id,
    };
    private static readonly Collection _collection = new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = _org1Id,
    };

    public static TheoryData<Func<IEventService, Task>, Func<IEventRepository, DateTime, Task>> TestData()
    {
        return new TheoryData<Func<IEventService, Task>, Func<IEventRepository, DateTime, Task>>
        {
            // General user event
            {
                (es) => es.LogUserEventAsync(_userInSingleOrg, EventType.User_LoggedIn),
                async (eventRepository, start) =>
                {
                    var eventsResult = await eventRepository.GetManyByOrganizationActingUserAsync(
                        _org1Id, _userInSingleOrg, start, DateTime.UtcNow, new PageOptions());

                    Assert.NotNull(eventsResult);
                    var orgEvent = Assert.Single(eventsResult.Data);
                    Assert.Equal(EventType.User_LoggedIn, orgEvent.Type);
                    Assert.Equal(DeviceType.EdgeBrowser, orgEvent.DeviceType);
                    Assert.Equal("1.1.1.1", orgEvent.IpAddress);
                    Assert.Equal(_userInSingleOrg, orgEvent.UserId);
                    Assert.Equal(_userInSingleOrg, orgEvent.ActingUserId);
                    Assert.Equal(_org1Id, orgEvent.OrganizationId);
                }
            },
            // User event for a user in multiple organizations
            {
                (es) => es.LogUserEventAsync(_userInTwoOrgs, EventType.User_ChangedPassword),
                async (eventRepository, start) =>
                {
                    var events = await eventRepository.GetManyByOrganizationActingUserAsync(
                        _org2Id, _userInTwoOrgs, start, DateTime.UtcNow, new PageOptions()
                    );

                    var orgEvent = Assert.Single(events.Data);

                    Assert.Equal(EventType.User_ChangedPassword, orgEvent.Type);
                    Assert.Equal(DeviceType.EdgeBrowser, orgEvent.DeviceType);
                    Assert.Equal("1.1.1.1", orgEvent.IpAddress);
                    Assert.Equal(_userInTwoOrgs, orgEvent.UserId);
                    Assert.Equal(_userInTwoOrgs, orgEvent.ActingUserId);
                    Assert.Equal(_org2Id, orgEvent.OrganizationId);

                    events = await eventRepository.GetManyByOrganizationActingUserAsync(
                        _org3Id, _userInTwoOrgs, start, DateTime.UtcNow, new PageOptions()
                    );

                    orgEvent = Assert.Single(events.Data);

                    Assert.Equal(EventType.User_ChangedPassword, orgEvent.Type);
                    Assert.Equal(DeviceType.EdgeBrowser, orgEvent.DeviceType);
                    Assert.Equal("1.1.1.1", orgEvent.IpAddress);
                    Assert.Equal(_userInTwoOrgs, orgEvent.UserId);
                    Assert.Equal(_userInTwoOrgs, orgEvent.ActingUserId);
                    Assert.Equal(_org3Id, orgEvent.OrganizationId);
                }
            },
            // Cipher event
            {
                (es) => es.LogCipherEventAsync(_cipher, EventType.Cipher_ClientAutofilled),
                async (eventRepository, start) =>
                {
                    var events = await eventRepository.GetManyByCipherAsync(_cipher, start, DateTime.UtcNow, new PageOptions());
                    var cipherEvent = Assert.Single(events.Data);

                    Assert.Equal(EventType.Cipher_ClientAutofilled, cipherEvent.Type);
                    Assert.Equal(DeviceType.EdgeBrowser, cipherEvent.DeviceType);
                    Assert.Equal("1.1.1.1", cipherEvent.IpAddress);
                    Assert.Null(cipherEvent.UserId);
                    Assert.Equal(_userInSingleOrg, cipherEvent.ActingUserId);
                    Assert.Equal(_org1Id, cipherEvent.OrganizationId);
                    Assert.Equal(_cipher.Id, cipherEvent.CipherId);
                }
            },
            // Collection event
            {
                (es) => es.LogCollectionEventAsync(_collection, EventType.Collection_Updated),
                async (er, start) =>
                {
                    var events = await er.GetManyByOrganizationAsync(_org1Id, start, DateTime.UtcNow, new PageOptions());

                    var orgEvent = Assert.Single(events.Data);

                    Assert.Equal(EventType.Collection_Updated, orgEvent.Type);
                    Assert.Equal(DeviceType.EdgeBrowser, orgEvent.DeviceType);
                    Assert.Equal("1.1.1.1", orgEvent.IpAddress);
                    Assert.Null(orgEvent.UserId);
                    Assert.Equal(_userInSingleOrg, orgEvent.ActingUserId);
                    Assert.Equal(_org1Id, orgEvent.OrganizationId);
                    Assert.Equal(_collection.Id, orgEvent.CollectionId);
                }
            }
        };
    }

    [RequiredEnvironmentTheory(EnvironmentVariableKey)]
    [MemberData(nameof(TestData))]
    [Trait("Environment", "Azure")]
    public async Task TestAzureQueueToAzureTableIntegration(Func<IEventService, Task> logger, Func<IEventRepository, DateTime, Task> asserter)
    {
        var startTime = DateTime.UtcNow;
        SetupContext("1.1.1.1", DeviceType.EdgeBrowser);

        await logger(_eventService);

        // TODO: One of the passed in loggers could log so much that it
        // isn't processed in a single call to this.
        // This should probably be changed to require that is processes something
        // on the first call but then it should call this until it doesn't
        // process anything more, then continue on to the asserter.
        var didProcess = await _processor.ProcessAsync(CancellationToken.None);
        Assert.True(didProcess);

        await asserter(_repository, startTime);
    }

    private void SetupContext(string ipAddress, DeviceType deviceType)
    {
        _currentContext.IpAddress = ipAddress;
        _currentContext.DeviceType = deviceType;
        // TODO: This hard ties this to a user
        _currentContext.UserId = _userInSingleOrg;
    }
}

// TODO: This could be a more general utility if we implemented scopes
file class XUnitTestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _testOutputHelper;

    public XUnitTestLogger(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _testOutputHelper.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}
