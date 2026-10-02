using System.Text;
using Bit.Core.Billing.Organizations.PlanMigration.Utilities;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Utilities;

public class CohortBulkAssignmentCsvParserTests
{
    private static IFormFile Csv(Stream stream) =>
        new FormFile(stream, 0, stream.Length, "File", "cohorts.csv");

    private readonly CohortBulkAssignmentCsvParser _sut = new();

    [Fact]
    public void Parse_ValidRows_ReturnsRowsNoErrors()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "OrganizationId,CohortName\n" +
            "11111111-1111-1111-1111-111111111111,A1 (a)\n"));
        var result = _sut.Parse(Csv(stream));

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.LineNumber);
        Assert.Equal("11111111-1111-1111-1111-111111111111", row.OrganizationId);
        Assert.Equal("A1 (a)", row.CohortName);
    }

    [Fact]
    public void Parse_EmptyCohortCell_IsSentinelNotError()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "OrganizationId,CohortName\n" +
            "11111111-1111-1111-1111-111111111111,\n"));
        var result = _sut.Parse(Csv(stream));

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(string.Empty, row.CohortName);
    }

    [Fact]
    public void Parse_MissingColumn_IsMalformedError()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "OrganizationId,CohortName\n" +
            "11111111-1111-1111-1111-111111111111\n"));
        var result = _sut.Parse(Csv(stream));

        Assert.Empty(result.Rows);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Contains("expected 2 columns", error.Message);
    }
}
