using Bit.Core.NotificationHub;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using static Bit.Core.Settings.GlobalSettings;

namespace Bit.Core.Test.NotificationHub;

public class NotificationHubPoolTests
{
    [Fact]
    public void NotificationHubPool_WarnsOnMissingConnectionString()
    {
        // Arrange
        var globalSettings = new GlobalSettings()
        {
            NotificationHubPool = new NotificationHubPoolSettings()
            {
                NotificationHubs = new() {
                    new() {
                        ConnectionString = null,
                        HubName = "hub",
                        RegistrationStartDate = DateTime.UtcNow,
                        RegistrationEndDate = DateTime.UtcNow.AddDays(1)
                    }
                }
            }
        };
        var logger = Substitute.For<ILogger<NotificationHubPool>>();

        // Act
        var sut = new NotificationHubPool(logger, globalSettings);

        // Assert
        logger.Received().Log(LogLevel.Warning, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString() == "Invalid notification hub settings: hub"),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public void NotificationHubPool_WarnsOnMissingHubName()
    {
        // Arrange
        var globalSettings = new GlobalSettings()
        {
            NotificationHubPool = new NotificationHubPoolSettings()
            {
                NotificationHubs = new() {
                    new() {
                        ConnectionString = "connection",
                        HubName = null,
                        RegistrationStartDate = DateTime.UtcNow,
                        RegistrationEndDate = DateTime.UtcNow.AddDays(1)
                    }
                }
            }
        };
        var logger = Substitute.For<ILogger<NotificationHubPool>>();

        // Act
        var sut = new NotificationHubPool(logger, globalSettings);

        // Assert
        logger.Received().Log(LogLevel.Warning, Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString() == "Invalid notification hub settings: hub name missing"),
            null,
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public void NotificationHubPool_ClientFor_ThrowsOnNoValidHubs()
    {
        // Arrange
        var globalSettings = new GlobalSettings()
        {
            NotificationHubPool = new NotificationHubPoolSettings()
            {
                NotificationHubs = new() {
                    new() {
                        ConnectionString = "connection",
                        HubName = "hub",
                        RegistrationStartDate = null,
                        RegistrationEndDate = null,
                    }
                }
            }
        };
        var logger = Substitute.For<ILogger<NotificationHubPool>>();
        var sut = new NotificationHubPool(logger, globalSettings);

        // Act
        Action act = () => sut.ClientFor(Guid.NewGuid());

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void NotificationHubPool_ClientFor_ReturnsClient()
    {
        // Arrange
        var globalSettings = new GlobalSettings()
        {
            NotificationHubPool = new NotificationHubPoolSettings()
            {
                NotificationHubs = new() {
                    new() {
                        ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;SharedAccessKey=example///example=",
                        HubName = "hub",
                        RegistrationStartDate = DateTime.UtcNow.AddMinutes(-1),
                        RegistrationEndDate = DateTime.UtcNow.AddDays(1),
                    }
                }
            }
        };
        var logger = Substitute.For<ILogger<NotificationHubPool>>();
        var sut = new NotificationHubPool(logger, globalSettings);

        // Act
        var client = sut.ClientFor(CoreHelpers.GenerateComb(Guid.NewGuid(), DateTime.UtcNow));

        // Assert
        Assert.NotNull(client);
    }

    [Fact]
    public void NotificationHubPool_AllClients_ReturnsProxy()
    {
        // Arrange
        var globalSettings = new GlobalSettings()
        {
            NotificationHubPool = new NotificationHubPoolSettings()
            {
                NotificationHubs = new() {
                    new() {
                        ConnectionString = "connection",
                        HubName = "hub",
                        RegistrationStartDate = DateTime.UtcNow,
                        RegistrationEndDate = DateTime.UtcNow.AddDays(1),
                    }
                }
            }
        };
        var logger = Substitute.For<ILogger<NotificationHubPool>>();
        var sut = new NotificationHubPool(logger, globalSettings);

        // Act
        var proxy = sut.AllClients;

        // Assert
        Assert.NotNull(proxy);
    }
}
