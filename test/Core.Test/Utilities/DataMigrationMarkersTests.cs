using Bit.Core.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class DataMigrationMarkersTests
{
    [Theory]
    [InlineData("Bit.Migrator.DbScripts.2026-10-07_00_DataMigration_ProtectApiKeys.sql", "ProtectApiKeys")]
    [InlineData("20261007000000_DataMigration_ProtectApiKeys", "ProtectApiKeys")]
    [InlineData("Bit.Migrator.DbScripts.2026-10-06_00_DataMigrationState.sql", null)]
    [InlineData("20261006070200_DataMigrationState", null)]
    public void GetName_ParsesMarkers(string migration, string? expected)
    {
        Assert.Equal(expected, DataMigrationMarkers.GetName(migration));
    }

    [Fact]
    public void Interleave_RunsEachDataMigrationAndAppliesItsMarkerWithTheNextSegment()
    {
        var (applied, ran) = (new List<string[]>(), new List<string>());

        var success = Interleave(["a", "b_DataMigration_X", "c", "d_DataMigration_Y"], applied, name =>
        {
            ran.Add(name);
            return true;
        });

        Assert.True(success);
        Assert.Equal(["X", "Y"], ran);
        Assert.Equal([["a"], ["b_DataMigration_X", "c"], ["d_DataMigration_Y"]], applied);
    }

    [Fact]
    public void Interleave_DataMigrationIncomplete_StopsBeforeItsMarker()
    {
        var applied = new List<string[]>();

        Assert.True(Interleave(["a", "b_DataMigration_X", "c"], applied, _ => false));

        Assert.Equal([["a"]], applied);
    }

    [Fact]
    public void Interleave_NoHandler_StopsAtTheFirstMarker()
    {
        var applied = new List<string[]>();

        Assert.True(Interleave(["b_DataMigration_X", "c"], applied, null));

        Assert.Empty(applied);
    }

    [Fact]
    public void Interleave_SegmentFails_StopsWithoutRunningTheDataMigration()
    {
        var ran = false;

        var success = DataMigrationMarkers.Interleave(["a", "b_DataMigration_X", "c"], _ => false, _ => ran = true,
            NullLogger.Instance);

        Assert.False(success);
        Assert.False(ran);
    }

    private static bool Interleave(string[] pending, List<string[]> applied, Func<string, bool>? onDataMigration) =>
        DataMigrationMarkers.Interleave(pending, segment =>
        {
            applied.Add(segment.ToArray());
            return true;
        }, onDataMigration, NullLogger.Instance);
}
