using System.Text.Json;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class LocalSendStorageServiceTests
{
    [Fact]
    public async Task DeleteFileAsync_FileExists_DeletesFileAndEmptyDirectory()
    {
        using var tempDirectory = new TempDirectory();
        var sut = CreateSut(tempDirectory);

        var send = new Send { Id = Guid.NewGuid(), Type = SendType.File };
        var fileId = "testfile";

        // Create the file on disk
        var dirPath = Path.Combine(tempDirectory.Directory, send.Id.ToString());
        Directory.CreateDirectory(dirPath);
        var filePath = Path.Combine(dirPath, fileId);
        await File.WriteAllTextAsync(filePath, "file contents");

        await sut.DeleteFileAsync(send, fileId);

        Assert.False(File.Exists(filePath));
        Assert.False(Directory.Exists(dirPath));
    }

    [Fact]
    public async Task DeleteFileAsync_FileDoesNotExist_DoesNotThrow()
    {
        using var tempDirectory = new TempDirectory();
        var sut = CreateSut(tempDirectory);

        var send = new Send { Id = Guid.NewGuid(), Type = SendType.File };

        await sut.DeleteFileAsync(send, "nonexistent");
    }

    [Fact]
    public async Task DeleteFileAsync_DirectoryHasOtherFiles_KeepsDirectory()
    {
        using var tempDirectory = new TempDirectory();
        var sut = CreateSut(tempDirectory);

        var send = new Send { Id = Guid.NewGuid(), Type = SendType.File };
        var fileId = "testfile";

        var dirPath = Path.Combine(tempDirectory.Directory, send.Id.ToString());
        Directory.CreateDirectory(dirPath);
        await File.WriteAllTextAsync(Path.Combine(dirPath, fileId), "delete me");
        await File.WriteAllTextAsync(Path.Combine(dirPath, "otherfile"), "keep me");

        await sut.DeleteFileAsync(send, fileId);

        Assert.False(File.Exists(Path.Combine(dirPath, fileId)));
        Assert.True(Directory.Exists(dirPath));
        Assert.True(File.Exists(Path.Combine(dirPath, "otherfile")));
    }

    [Fact]
    public async Task DeleteFilesForUserAsync_WithFileSends_DeletesFiles()
    {
        using var tempDirectory = new TempDirectory();
        var sendRepository = Substitute.For<ISendRepository>();
        var sut = CreateSut(tempDirectory, sendRepository);

        var userId = Guid.NewGuid();
        var send1 = new Send { Id = Guid.NewGuid(), UserId = userId, Type = SendType.File };
        var send2 = new Send { Id = Guid.NewGuid(), UserId = userId, Type = SendType.File };
        var fileId1 = "file1";
        var fileId2 = "file2";
        send1.Data = JsonSerializer.Serialize(new SendFileData { Id = fileId1, FileName = "a.txt" });
        send2.Data = JsonSerializer.Serialize(new SendFileData { Id = fileId2, FileName = "b.txt" });

        // Create files on disk
        var dir1 = Path.Combine(tempDirectory.Directory, send1.Id.ToString());
        var dir2 = Path.Combine(tempDirectory.Directory, send2.Id.ToString());
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);
        await File.WriteAllTextAsync(Path.Combine(dir1, fileId1), "contents1");
        await File.WriteAllTextAsync(Path.Combine(dir2, fileId2), "contents2");

        sendRepository.GetManyFileSendsByUserIdAsync(userId).Returns(new List<Send> { send1, send2 });

        await sut.DeleteFilesForUserAsync(userId);

        Assert.False(File.Exists(Path.Combine(dir1, fileId1)));
        Assert.False(Directory.Exists(dir1));
        Assert.False(File.Exists(Path.Combine(dir2, fileId2)));
        Assert.False(Directory.Exists(dir2));
    }

    [Fact]
    public async Task DeleteFilesForUserAsync_NoFileSends_DoesNothing()
    {
        using var tempDirectory = new TempDirectory();
        var sendRepository = Substitute.For<ISendRepository>();
        var sut = CreateSut(tempDirectory, sendRepository);

        var userId = Guid.NewGuid();
        var textSend = new Send { Id = Guid.NewGuid(), UserId = userId, Type = SendType.Text };

        sendRepository.GetManyFileSendsByUserIdAsync(userId).Returns(new List<Send> { textSend });

        await sut.DeleteFilesForUserAsync(userId);

        // No files should have been touched — base directory should still be empty
        Assert.Empty(Directory.GetDirectories(tempDirectory.Directory));
    }

    [Fact]
    public async Task DeleteFilesForUserAsync_NoSends_DoesNotThrow()
    {
        using var tempDirectory = new TempDirectory();
        var sendRepository = Substitute.For<ISendRepository>();
        var sut = CreateSut(tempDirectory, sendRepository);

        var userId = Guid.NewGuid();
        sendRepository.GetManyFileSendsByUserIdAsync(userId).Returns(new List<Send>());

        await sut.DeleteFilesForUserAsync(userId);
    }

    [Fact]
    public async Task DeleteFilesForUserAsync_MalformedData_LogsWarningAndContinues()
    {
        using var tempDirectory = new TempDirectory();
        var sendRepository = Substitute.For<ISendRepository>();
        var sut = CreateSut(tempDirectory, sendRepository);

        var userId = Guid.NewGuid();
        var badSend = new Send { Id = Guid.NewGuid(), UserId = userId, Type = SendType.File, Data = "not valid json{{{" };
        var goodSend = new Send { Id = Guid.NewGuid(), UserId = userId, Type = SendType.File };
        var fileId = "goodfile";
        goodSend.Data = JsonSerializer.Serialize(new SendFileData { Id = fileId, FileName = "c.txt" });

        // Create file for the good send
        var dir = Path.Combine(tempDirectory.Directory, goodSend.Id.ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, fileId), "contents");

        sendRepository.GetManyFileSendsByUserIdAsync(userId).Returns(new List<Send> { badSend, goodSend });

        await sut.DeleteFilesForUserAsync(userId);

        // Good send's file should still be deleted despite the bad send
        Assert.False(File.Exists(Path.Combine(dir, fileId)));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task DeleteFilesForOrganizationAsync_WithFileSends_DeletesFiles()
    {
        using var tempDirectory = new TempDirectory();
        var sendRepository = Substitute.For<ISendRepository>();
        var sut = CreateSut(tempDirectory, sendRepository);

        var orgId = Guid.NewGuid();
        var send = new Send { Id = Guid.NewGuid(), OrganizationId = orgId, Type = SendType.File };
        var fileId = "orgfile";
        send.Data = JsonSerializer.Serialize(new SendFileData { Id = fileId, FileName = "d.txt" });

        var dir = Path.Combine(tempDirectory.Directory, send.Id.ToString());
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, fileId), "org contents");

        sendRepository.GetManyFileSendsByOrganizationIdAsync(orgId).Returns(new List<Send> { send });

        await sut.DeleteFilesForOrganizationAsync(orgId);

        Assert.False(File.Exists(Path.Combine(dir, fileId)));
        Assert.False(Directory.Exists(dir));
    }

    private static LocalSendStorageService CreateSut(TempDirectory tempDirectory,
        ISendRepository? sendRepository = null)
    {
        var globalSettings = new Bit.Core.Settings.GlobalSettings();
        globalSettings.Send.BaseDirectory = tempDirectory.Directory;
        globalSettings.Send.BaseUrl = "https://localhost/sends";
        return new LocalSendStorageService(
            globalSettings,
            sendRepository ?? Substitute.For<ISendRepository>(),
            Substitute.For<ILogger<LocalSendStorageService>>());
    }
}
