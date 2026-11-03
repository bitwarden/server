using System.Text;
using Bit.Api.Utilities;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class MultipartFormDataHelperTests
{
    private const string _boundary = "test-boundary";

    private static HttpRequest BuildRequest(params (string Name, string FileName, string Value)[] parts)
    {
        var body = new StringBuilder();
        foreach (var (name, fileName, value) in parts)
        {
            var disposition = fileName == null
                ? "form-data; name=\"" + name + "\""
                : "form-data; name=\"" + name + "\"; filename=\"" + fileName + "\"";
            body.Append("--" + _boundary + "\r\n");
            body.Append("Content-Disposition: " + disposition + "\r\n\r\n");
            body.Append(value + "\r\n");
        }
        body.Append("--" + _boundary + "--\r\n");

        var context = new DefaultHttpContext();
        context.Request.ContentType = $"multipart/form-data; boundary={_boundary}";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body.ToString()));
        return context.Request;
    }

    [Fact]
    public async Task GetFileAsync_KeyAndRevisionDateBeforeFile_PassesAllValuesToCallback()
    {
        var request = BuildRequest(
            ("key", null, "the-key"),
            ("lastKnownRevisionDate", null, "2025-10-22T12:00:00.000Z"),
            ("data", "file.txt", "file-contents"));

        string capturedFileName = null, capturedKey = null, capturedDate = null, capturedContents = null;
        await request.GetFileAsync(async (stream, fileName, key, lastKnownRevisionDate) =>
        {
            capturedFileName = fileName;
            capturedKey = key;
            capturedDate = lastKnownRevisionDate;
            using var reader = new StreamReader(stream);
            capturedContents = await reader.ReadToEndAsync();
        });

        Assert.Equal("file.txt", capturedFileName);
        Assert.Equal("the-key", capturedKey);
        Assert.Equal("2025-10-22T12:00:00.000Z", capturedDate);
        Assert.Equal("file-contents", capturedContents);
    }

    [Fact]
    public async Task GetFileAsync_RevisionDateBeforeKey_PassesAllValuesToCallback()
    {
        var request = BuildRequest(
            ("lastKnownRevisionDate", null, "2025-10-22T12:00:00.000Z"),
            ("key", null, "the-key"),
            ("data", "file.txt", "file-contents"));

        string capturedKey = null, capturedDate = null;
        await request.GetFileAsync((stream, fileName, key, lastKnownRevisionDate) =>
        {
            capturedKey = key;
            capturedDate = lastKnownRevisionDate;
            return Task.CompletedTask;
        });

        Assert.Equal("the-key", capturedKey);
        Assert.Equal("2025-10-22T12:00:00.000Z", capturedDate);
    }

    [Fact]
    public async Task GetFileAsync_NoRevisionDate_PassesNullDate()
    {
        var request = BuildRequest(
            ("key", null, "the-key"),
            ("data", "file.txt", "file-contents"));

        var called = false;
        string capturedDate = "unset";
        await request.GetFileAsync((stream, fileName, key, lastKnownRevisionDate) =>
        {
            called = true;
            capturedDate = lastKnownRevisionDate;
            return Task.CompletedTask;
        });

        Assert.True(called);
        Assert.Null(capturedDate);
    }

    [Fact]
    public async Task GetFileAsync_NoFileSection_DoesNotInvokeCallback()
    {
        var request = BuildRequest(("key", null, "the-key"));

        var called = false;
        await request.GetFileAsync((stream, fileName, key, lastKnownRevisionDate) =>
        {
            called = true;
            return Task.CompletedTask;
        });

        Assert.False(called);
    }
}
