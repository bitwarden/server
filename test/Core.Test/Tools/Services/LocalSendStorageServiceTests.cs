using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Services;
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

    private static LocalSendStorageService CreateSut(TempDirectory tempDirectory)
    {
        var globalSettings = new Bit.Core.Settings.GlobalSettings();
        globalSettings.Send.BaseDirectory = tempDirectory.Directory;
        globalSettings.Send.BaseUrl = "https://localhost/sends";
        return new LocalSendStorageService(globalSettings);
    }
}
