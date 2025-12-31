using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class SendOwnerQueryTests
{
    private readonly ISendRepository _sendRepository;
    private readonly IFeatureService _featureService;
    private readonly IUserService _userService;
    private readonly SendOwnerQuery _sendOwnerQuery;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ClaimsPrincipal _user;

    public SendOwnerQueryTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _featureService = Substitute.For<IFeatureService>();
        _userService = Substitute.For<IUserService>();
        _user = new ClaimsPrincipal();
        _userService.GetProperUserId(_user).Returns(_currentUserId);
        _sendOwnerQuery = new SendOwnerQuery(_sendRepository, _featureService, _userService);
    }

    [Fact]
    public async Task Get_WithValidSendOwnedByUser_ReturnsExpectedSend()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var expectedSend = CreateSend(sendId, _currentUserId);
        _sendRepository.GetByIdAsync(sendId).Returns(expectedSend);

        // Act
        var result = await _sendOwnerQuery.Get(sendId, _user);

        // Assert
        Assert.Same(expectedSend, result);
        await _sendRepository.Received(1).GetByIdAsync(sendId);
    }

    [Fact]
    public async Task Get_WithNonExistentSend_ThrowsNotFoundException()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        _sendRepository.GetByIdAsync(sendId).Returns((Send?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sendOwnerQuery.Get(sendId, _user));
    }

    [Fact]
    public async Task Get_WithSendOwnedByDifferentUser_ThrowsNotFoundException()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var send = CreateSend(sendId, differentUserId);
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sendOwnerQuery.Get(sendId, _user));
    }

    [Fact]
    public async Task Get_WithNullCurrentUserId_ThrowsBadRequestException()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = CreateSend(sendId, _currentUserId);
        _sendRepository.GetByIdAsync(sendId).Returns(send);
        var nullUser = new ClaimsPrincipal();
        _userService.GetProperUserId(nullUser).Returns((Guid?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sendOwnerQuery.Get(sendId, nullUser));
        Assert.Equal("invalid user.", exception.Message);
    }

    [Fact]
    public async Task GetOwned_WithFeatureFlagEnabled_ReturnsAllSends()
    {
        // Arrange
        var sends = new List<Send>
        {
            CreateSend(Guid.NewGuid(), _currentUserId, emails: null),
            CreateSend(Guid.NewGuid(), _currentUserId, emails: "test@example.com"),
            CreateSend(Guid.NewGuid(), _currentUserId, emails: "other@example.com")
        };
        _sendRepository.GetManyByUserIdAsync(_currentUserId).Returns(sends);
        _featureService.IsEnabled(FeatureFlagKeys.PM19051_ListEmailOtpSends).Returns(true);

        // Act
        var result = await _sendOwnerQuery.GetOwned(_user);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(sends[0], result);
        Assert.Contains(sends[1], result);
        Assert.Contains(sends[2], result);
        await _sendRepository.Received(1).GetManyByUserIdAsync(_currentUserId);
        _featureService.Received(1).IsEnabled(FeatureFlagKeys.PM19051_ListEmailOtpSends);
    }

    [Fact]
    public async Task GetOwned_WithFeatureFlagDisabled_FiltersOutEmailOtpSends()
    {
        // Arrange
        var sendWithoutEmails = CreateSend(Guid.NewGuid(), _currentUserId, emails: null);
        var sendWithEmails = CreateSend(Guid.NewGuid(), _currentUserId, emails: "test@example.com");
        var sends = new List<Send> { sendWithoutEmails, sendWithEmails };
        _sendRepository.GetManyByUserIdAsync(_currentUserId).Returns(sends);
        _featureService.IsEnabled(FeatureFlagKeys.PM19051_ListEmailOtpSends).Returns(false);

        // Act
        var result = await _sendOwnerQuery.GetOwned(_user);

        // Assert
        Assert.Single(result);
        Assert.Contains(sendWithoutEmails, result);
        Assert.DoesNotContain(sendWithEmails, result);
        await _sendRepository.Received(1).GetManyByUserIdAsync(_currentUserId);
        _featureService.Received(1).IsEnabled(FeatureFlagKeys.PM19051_ListEmailOtpSends);
    }

    [Fact]
    public async Task GetOwned_WithNullCurrentUserId_ThrowsBadRequestException()
    {
        // Arrange
        var nullUser = new ClaimsPrincipal();
        _userService.GetProperUserId(nullUser).Returns((Guid?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _sendOwnerQuery.GetOwned(nullUser));
        Assert.Equal("invalid user.", exception.Message);
    }

    [Fact]
    public async Task GetOwned_WithEmptyCollection_ReturnsEmptyCollection()
    {
        // Arrange
        var emptySends = new List<Send>();
        _sendRepository.GetManyByUserIdAsync(_currentUserId).Returns(emptySends);
        _featureService.IsEnabled(FeatureFlagKeys.PM19051_ListEmailOtpSends).Returns(true);

        // Act
        var result = await _sendOwnerQuery.GetOwned(_user);

        // Assert
        Assert.Empty(result);
        await _sendRepository.Received(1).GetManyByUserIdAsync(_currentUserId);
    }

    private static Send CreateSend(Guid id, Guid userId, string? emails = null)
    {
        return new Send
        {
            Id = id,
            UserId = userId,
            Emails = emails
        };
    }
}
