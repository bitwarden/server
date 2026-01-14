using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Xunit;

namespace Bit.Core.Test.Auth.Entities;

public class AuthRequestTests
{
    [Fact]
    public void IsValidForAuthentication_WithValidRequest_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = true,
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidForAuthentication_WithWrongUserId_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = true,
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(differentUserId, accessCode);

        // Assert
        Assert.False(result, "Auth request should not validate for a different user");
    }

    [Fact]
    public void IsValidForAuthentication_WithWrongAccessCode_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = true,
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = "correct-code"
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, "wrong-code");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidForAuthentication_WithoutResponseDate_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = null, // Not responded to
            Approved = true,
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.False(result, "Unanswered auth requests should not be valid");
    }

    [Fact]
    public void IsValidForAuthentication_WithApprovedFalse_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = false, // Denied
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.False(result, "Denied auth requests should not be valid");
    }

    [Fact]
    public void IsValidForAuthentication_WithApprovedNull_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = null, // Pending
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.False(result, "Pending auth requests should not be valid");
    }

    [Fact]
    public void IsValidForAuthentication_WithExpiredRequest_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = true,
            CreationDate = DateTime.UtcNow.AddMinutes(-20), // Expired (15 min timeout)
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.False(result, "Expired auth requests should not be valid");
    }

    [Fact]
    public void IsValidForAuthentication_WithWrongType_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.Unlock, // Wrong type
            ResponseDate = DateTime.UtcNow,
            Approved = true,
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = null,
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.False(result, "Only AuthenticateAndUnlock type should be valid");
    }

    [Fact]
    public void IsValidForAuthentication_WithAlreadyUsed_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accessCode = "test-access-code";
        var authRequest = new AuthRequest
        {
            UserId = userId,
            Type = AuthRequestType.AuthenticateAndUnlock,
            ResponseDate = DateTime.UtcNow,
            Approved = true,
            CreationDate = DateTime.UtcNow,
            AuthenticationDate = DateTime.UtcNow, // Already used
            AccessCode = accessCode
        };

        // Act
        var result = authRequest.IsValidForAuthentication(userId, accessCode);

        // Assert
        Assert.False(result, "Auth requests should only be valid for one-time use");
    }
}
