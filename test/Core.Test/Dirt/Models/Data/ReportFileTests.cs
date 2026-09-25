using System.Text.Json;
using Bit.Core.Dirt.Models.Data;
using Xunit;

namespace Bit.Core.Test.Dirt.Models.Data;

public class ReportFileTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var data = new ReportFile();

        Assert.Null(data.Id);
        Assert.Equal(string.Empty, data.FileName);
        Assert.Equal(0, data.Size);
        Assert.False(data.Validated);
    }

    [Fact]
    public void Serialize_RoundTrip_PreservesAllProperties()
    {
        var original = new ReportFile
        {
            Id = "file-123",
            FileName = "report.json",
            Size = 1048576,
            Validated = false
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ReportFile>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.FileName, deserialized.FileName);
        Assert.Equal(original.Size, deserialized.Size);
        Assert.Equal(original.Validated, deserialized.Validated);
    }

    [Fact]
    public void Serialize_SizeIsWrittenAsString()
    {
        var data = new ReportFile
        {
            Id = "file-456",
            FileName = "report.json",
            Size = 9876543210
        };

        var json = JsonSerializer.Serialize(data);

        Assert.Contains("\"9876543210\"", json);
    }

    [Fact]
    public void Deserialize_SizeCanBeReadFromString()
    {
        var json = """{"Id":"file-789","FileName":"test.json","Size":"5000","Validated":true}""";

        var data = JsonSerializer.Deserialize<ReportFile>(json);

        Assert.NotNull(data);
        Assert.Equal(5000, data.Size);
    }
}
