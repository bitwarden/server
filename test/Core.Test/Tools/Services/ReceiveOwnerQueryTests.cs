using System.Security.Claims;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReceiveFeatures.Queries;
using Bit.Core.Tools.Repositories;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class ReceiveOwnerQueryTests
{
    private readonly IReceiveRepository _receiveRepository;
    private readonly IUserService _userService;
    private readonly ReceiveOwnerQuery _receiveOwnerQuery;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ClaimsPrincipal _user;

    public ReceiveOwnerQueryTests()
    {
        _receiveRepository = Substitute.For<IReceiveRepository>();
        _userService = Substitute.For<IUserService>();
        _user = new ClaimsPrincipal();
        _userService.GetProperUserId(_user).Returns(_currentUserId);
        _receiveOwnerQuery = new ReceiveOwnerQuery(_receiveRepository, _userService);
    }

    [Fact]
    public async Task Get_WithValidReceiveOwnedByUser_ReturnsExpectedReceive()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var expectedReceive = CreateReceive(receiveId, _currentUserId);
        _receiveRepository.GetByIdAsync(receiveId).Returns(expectedReceive);

        // Act
        var result = await _receiveOwnerQuery.Get(receiveId, _user);

        // Assert
        Assert.Same(expectedReceive, result);
        await _receiveRepository.Received(1).GetByIdAsync(receiveId);
    }

    [Fact]
    public async Task Get_WithNonExistentReceive_ThrowsNotFoundException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        _receiveRepository.GetByIdAsync(receiveId).Returns((Receive?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _receiveOwnerQuery.Get(receiveId, _user));
    }

    [Fact]
    public async Task Get_WithReceiveOwnedByDifferentUser_ThrowsNotFoundException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var receive = CreateReceive(receiveId, differentUserId);
        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _receiveOwnerQuery.Get(receiveId, _user));
    }

    [Fact]
    public async Task Get_WithNullCurrentUserId_ThrowsBadRequestException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var receive = CreateReceive(receiveId, _currentUserId);
        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);
        var nullUser = new ClaimsPrincipal();
        _userService.GetProperUserId(nullUser).Returns((Guid?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _receiveOwnerQuery.Get(receiveId, nullUser));
        Assert.Equal("invalid user.", exception.Message);
    }

    [Fact]
    public async Task GetOwned_ReturnsAllReceives()
    {
        // Arrange
        var receives = new List<Receive>
        {
            CreateReceive(Guid.NewGuid(), _currentUserId),
            CreateReceive(Guid.NewGuid(), _currentUserId),
            CreateReceive(Guid.NewGuid(), _currentUserId)
        };
        _receiveRepository.GetManyByUserIdAsync(_currentUserId).Returns(receives);

        // Act
        var result = await _receiveOwnerQuery.GetOwned(_user);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(receives[0], result);
        Assert.Contains(receives[1], result);
        Assert.Contains(receives[2], result);
        await _receiveRepository.Received(1).GetManyByUserIdAsync(_currentUserId);
    }

    [Fact]
    public async Task GetOwned_WithNullCurrentUserId_ThrowsBadRequestException()
    {
        // Arrange
        var nullUser = new ClaimsPrincipal();
        _userService.GetProperUserId(nullUser).Returns((Guid?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(() => _receiveOwnerQuery.GetOwned(nullUser));
        Assert.Equal("invalid user.", exception.Message);
    }

    [Fact]
    public async Task GetOwned_WithEmptyCollection_ReturnsEmptyCollection()
    {
        // Arrange
        var emptyReceives = new List<Receive>();
        _receiveRepository.GetManyByUserIdAsync(_currentUserId).Returns(emptyReceives);

        // Act
        var result = await _receiveOwnerQuery.GetOwned(_user);

        // Assert
        Assert.Empty(result);
        await _receiveRepository.Received(1).GetManyByUserIdAsync(_currentUserId);
    }

    private static Receive CreateReceive(Guid id, Guid userId)
    {
        return new Receive
        {
            Id = id,
            UserId = userId,
            Name = "test-name",
            Data = "{}",
            UserKeyWrappedSharedContentEncryptionKey = "test-key",
            UserKeyWrappedPrivateKey = "test-private-key",
            ScekWrappedPublicKey = "test-public-key",
            Secret = "test-secret"
        };
    }
}
