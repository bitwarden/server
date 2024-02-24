using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Bit.Core.Test.Repositories.TableStorage;

public class AzureTablesEventRepositoryTests
{

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IProviderUserRepository _providerUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly ICurrentContext _currentContext;
    private readonly GlobalSettings _globalSettings;

    private readonly IEventService _eventService;
    private readonly ITestOutputHelper _output;

    public AzureTablesEventRepositoryTests(ITestOutputHelper output)
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

        _output = output;
    }

    [Fact] // Skip if environment variable isn't there
    [Trait("Container", "Storage")]
    public async Task DoThings()
    {
        SetupContext("1.1.1.1", DeviceType.EdgeBrowser);

        await _eventService.LogUserEventAsync(Guid.NewGuid(), EventType.User_LoggedIn);
    }

    private void SetupContext(string ipAddress, DeviceType deviceType)
    {
        _currentContext.IpAddress = ipAddress;
        _currentContext.DeviceType = deviceType;
    }
}
