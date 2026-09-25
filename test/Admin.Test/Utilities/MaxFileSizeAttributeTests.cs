using Bit.Admin.Utilities;
using Bit.Core;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Admin.Test.Utilities;

public class MaxFileSizeAttributeTests
{
    private static IFormFile FileOfLength(long length)
    {
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(length);
        return file;
    }

    [Fact]
    public void IsValid_NullFile_ReturnsTrue()
    {
        var sut = new MaxFileSizeAttribute(Constants.FileSize25mb);
        Assert.True(sut.IsValid(null));
    }

    [Fact]
    public void IsValid_FileAtOrUnderLimit_ReturnsTrue()
    {
        var sut = new MaxFileSizeAttribute(Constants.FileSize25mb);
        Assert.True(sut.IsValid(FileOfLength(Constants.FileSize25mb)));
    }

    [Fact]
    public void IsValid_FileOverLimit_ReturnsFalse()
    {
        var sut = new MaxFileSizeAttribute(Constants.FileSize25mb);
        Assert.False(sut.IsValid(FileOfLength(Constants.FileSize25mb + 1)));
    }
}
