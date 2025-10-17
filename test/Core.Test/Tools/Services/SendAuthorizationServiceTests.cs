using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class SendAuthorizationServiceTests
{
    private readonly ISendRepository _sendRepository;
    private readonly IPasswordHasher<Bit.Core.Entities.User> _passwordHasher;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly SendAuthorizationService _sendAuthorizationService;

    public SendAuthorizationServiceTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher<Bit.Core.Entities.User>>();
        _pushNotificationService = Substitute.For<IPushNotificationService>();

        _sendAuthorizationService = new SendAuthorizationService(
            _sendRepository,
            _passwordHasher,
            _pushNotificationService);
    }


    [Fact]
    public void SendCanBeAccessed_Success_ReturnsTrue()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MaxAccessCount = 10,
            AccessCount = 5,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            DeletionDate = DateTime.UtcNow.AddYears(1),
            Disabled = false,
            Password = "hashedPassword123"
        };

        const string password = "TEST";

        _passwordHasher
            .VerifyHashedPassword(Arg.Any<Bit.Core.Entities.User>(), send.Password, password)
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result =
            _sendAuthorizationService.SendCanBeAccessed(send, password);

        // Assert
        Assert.Equal(SendAccessResult.Granted, result);
    }

    [Fact]
    public void SendCanBeAccessed_NullMaxAccess_Success()
    {
        // Arrange
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MaxAccessCount = null,
            AccessCount = 5,
            ExpirationDate = DateTime.UtcNow.AddYears(1),
            DeletionDate = DateTime.UtcNow.AddYears(1),
            Disabled = false,
            Password = "hashedPassword123"
        };

        const string password = "TEST";

        _passwordHasher
            .VerifyHashedPassword(Arg.Any<Bit.Core.Entities.User>(), send.Password, password)
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result = _sendAuthorizationService.SendCanBeAccessed(send, password);

        // Assert
        Assert.Equal(SendAccessResult.Granted, result);
    }

    [Fact]
    public void SendCanBeAccessed_NullSend_DoesNotGrantAccess()
    {
        // Arrange
        _passwordHasher
            .VerifyHashedPassword(Arg.Any<Bit.Core.Entities.User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.Success);

        // Act
        var result =
            _sendAuthorizationService.SendCanBeAccessed(null, "TEST");

        // Assert
        Assert.Equal(SendAccessResult.Denied, result);
    }

    [Fact]
    public void SendCanBeAccessed_RehashNeeded_RehashesPassword()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MaxAccessCount = null,
            AccessCount = 5,
            ExpirationDate = now.AddYears(1),
            DeletionDate = now.AddYears(1),
            Disabled = false,
            Password = "TEST"
        };

        _passwordHasher
            .VerifyHashedPassword(Arg.Any<Bit.Core.Entities.User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);

        // Act
        var result =
            _sendAuthorizationService.SendCanBeAccessed(send, "TEST");

        // Assert
        _passwordHasher
            .Received(1)
            .HashPassword(Arg.Any<Bit.Core.Entities.User>(), "TEST");

        Assert.Equal(SendAccessResult.Granted, result);
    }

    [Fact]
    public void SendCanBeAccessed_VerifyFailed_PasswordInvalidReturnsTrue()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var send = new Send
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MaxAccessCount = null,
            AccessCount = 5,
            ExpirationDate = now.AddYears(1),
            DeletionDate = now.AddYears(1),
            Disabled = false,
            Password = "TEST"
        };

        _passwordHasher
            .VerifyHashedPassword(Arg.Any<Bit.Core.Entities.User>(), "TEST", "TEST")
            .Returns(PasswordVerificationResult.Failed);

        // Act
        var result =
            _sendAuthorizationService.SendCanBeAccessed(send, "TEST");

        // Assert
        Assert.Equal(SendAccessResult.PasswordInvalid, result);
    }
}
