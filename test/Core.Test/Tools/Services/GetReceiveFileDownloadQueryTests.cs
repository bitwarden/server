using System.Security.Claims;
using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReceiveFeatures.Queries;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class GetReceiveFileDownloadQueryTests
{
    private readonly IReceiveRepository _receiveRepository;
    private readonly IUserService _userService;
    private readonly IReceiveFileStorageService _fileStorageService;
    private readonly GetReceiveFileDownloadQuery _query;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ClaimsPrincipal _user;

    public GetReceiveFileDownloadQueryTests()
    {
        _receiveRepository = Substitute.For<IReceiveRepository>();
        _userService = Substitute.For<IUserService>();
        _fileStorageService = Substitute.For<IReceiveFileStorageService>();
        _user = new ClaimsPrincipal();
        _userService.GetProperUserId(_user).Returns(_currentUserId);
        _query = new GetReceiveFileDownloadQuery(_receiveRepository, _userService, _fileStorageService);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_ValidOwnedReceiveWithFile_ReturnsUrl()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var fileId = "abc123fileId";
        var receive = CreateReceive(receiveId, _currentUserId, fileId);
        var expectedUrl = "https://storage.example.com/receivefiles/download?sastoken";

        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);
        _fileStorageService.GetReceiveFileDownloadUrlAsync(receive, fileId).Returns(expectedUrl);

        // Act
        var url = await _query.GetDownloadUrlAsync(receiveId, fileId, _user);

        // Assert
        Assert.Equal(expectedUrl, url);
        await _fileStorageService.Received(1).GetReceiveFileDownloadUrlAsync(receive, fileId);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_MultipleFiles_ReturnsCorrectFileUrl()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var fileId1 = "file1";
        var fileId2 = "file2";
        var receive = CreateReceive(receiveId, _currentUserId, fileId1, fileId2);
        var expectedUrl = "https://storage.example.com/file2?sastoken";

        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);
        _fileStorageService.GetReceiveFileDownloadUrlAsync(receive, fileId2).Returns(expectedUrl);

        // Act
        var url = await _query.GetDownloadUrlAsync(receiveId, fileId2, _user);

        // Assert
        Assert.Equal(expectedUrl, url);
        await _fileStorageService.Received(1).GetReceiveFileDownloadUrlAsync(receive, fileId2);
    }

    [Fact]
    public async Task GetDownloadUrlAsync_NonExistentReceive_ThrowsNotFoundException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        _receiveRepository.GetByIdAsync(receiveId).Returns((Receive?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _query.GetDownloadUrlAsync(receiveId, "anyFileId", _user));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_ReceiveOwnedByDifferentUser_ThrowsNotFoundException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var receive = CreateReceive(receiveId, differentUserId, "someFileId");

        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _query.GetDownloadUrlAsync(receiveId, "someFileId", _user));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_FileNotInReceive_ThrowsNotFoundException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var receive = CreateReceive(receiveId, _currentUserId, "existingFileId");

        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _query.GetDownloadUrlAsync(receiveId, "nonExistentFileId", _user));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_NoFiles_ThrowsNotFoundException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var receive = CreateReceive(receiveId, _currentUserId);

        _receiveRepository.GetByIdAsync(receiveId).Returns(receive);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _query.GetDownloadUrlAsync(receiveId, "anyFileId", _user));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_InvalidUser_ThrowsBadRequestException()
    {
        // Arrange
        var receiveId = Guid.NewGuid();
        var nullUser = new ClaimsPrincipal();
        _userService.GetProperUserId(nullUser).Returns((Guid?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => _query.GetDownloadUrlAsync(receiveId, "anyFileId", nullUser));
        Assert.Equal("invalid user.", exception.Message);
    }

    private static Receive CreateReceive(Guid id, Guid userId, params string[] fileIds)
    {
        var receiveData = new ReceiveData
        {
            Files = fileIds.Select(fid => new ReceiveFileData { Id = fid, Validated = true }).ToList()
        };
        return new Receive
        {
            Id = id,
            UserId = userId,
            Name = "test-name",
            Data = JsonSerializer.Serialize(receiveData),
            UserKeyWrappedSharedContentEncryptionKey = "test-key",
            UserKeyWrappedPrivateKey = "test-private-key",
            ScekWrappedPublicKey = "test-public-key",
            Secret = "test-secret"
        };
    }
}
