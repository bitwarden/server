using Bit.Core.Settings;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

public class NotificationHubConnectionTests
{
    [Fact]
    public void IsValid_ConnectionStringIsNull_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = null,
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var connection = NotificationHubConnection.From(hub);

        // Assert
        Assert.False(connection.IsValid);
    }

    [Fact]
    public void IsValid_HubNameIsNull_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "Endpoint=sb://example.servicebus.windows.net/;",
            HubName = null,
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var connection = NotificationHubConnection.From(hub);

        // Assert
        Assert.False(connection.IsValid);
    }

    [Fact]
    public void IsValid_ConnectionStringAndHubNameAreNotNull_ReturnsTrue()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var connection = NotificationHubConnection.From(hub);

        // Assert
        Assert.True(connection.IsValid);
    }

    [Fact]
    public void RegistrationEnabled_QueryTimeIsBeforeStartDate_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow.AddDays(1),
            RegistrationEndDate = DateTime.UtcNow.AddDays(2)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(DateTime.UtcNow);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RegistrationEnabled_QueryTimeIsAfterEndDate_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(DateTime.UtcNow.AddDays(2));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RegistrationEnabled_NullStartDate_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = null,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(DateTime.UtcNow);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RegistrationEnabled_QueryTimeIsBetweenStartDateAndEndDate_ReturnsTrue()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(DateTime.UtcNow.AddHours(1));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RegistrationEnabled_CombTimeIsBeforeStartDate_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow.AddDays(1),
            RegistrationEndDate = DateTime.UtcNow.AddDays(2)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(CoreHelpers.GenerateComb(Guid.NewGuid(), DateTime.UtcNow));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RegistrationEnabled_CombTimeIsAfterEndDate_ReturnsFalse()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(CoreHelpers.GenerateComb(Guid.NewGuid(), DateTime.UtcNow.AddDays(2)));

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RegistrationEnabled_CombTimeIsBetweenStartDateAndEndDate_ReturnsTrue()
    {
        // Arrange
        var hub = new GlobalSettings.NotificationHubSettings()
        {
            ConnectionString = "connection",
            HubName = "hub",
            RegistrationStartDate = DateTime.UtcNow,
            RegistrationEndDate = DateTime.UtcNow.AddDays(1)
        };
        var connection = NotificationHubConnection.From(hub);

        // Act
        var result = connection.RegistrationEnabled(CoreHelpers.GenerateComb(Guid.NewGuid(), DateTime.UtcNow.AddHours(1)));

        // Assert
        Assert.True(result);
    }
}
