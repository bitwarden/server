using Bit.Core.Exceptions;
using Bit.Services.Pam.Rotation;
using Xunit;

namespace Bit.Services.Pam.Test.Rotation.Services;

public class RotationScheduleCalculatorTests
{
    private static readonly DateTime _afterUtc = new(2026, 7, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly RotationScheduleCalculator _sut = new();

    [Fact]
    public void GetNextOccurrence_ValidSixFieldCron_ReturnsNextOccurrence()
    {
        // Every 15 minutes; after 12:07 the next run is 12:15. A quarter-hour cadence is time-zone-proof (every
        // real-world UTC offset is a multiple of 15 minutes), unlike a day-anchored cron -- see the skipped test
        // below.
        var after = new DateTime(2026, 7, 6, 12, 7, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("0 0/15 * * * ?", after);

        Assert.Equal(new DateTime(2026, 7, 6, 12, 15, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void GetNextOccurrence_AtExactOccurrence_ReturnsStrictlyAfter()
    {
        var atQuarterHour = new DateTime(2026, 7, 6, 12, 15, 0, DateTimeKind.Utc);

        var next = _sut.GetNextOccurrence("0 0/15 * * * ?", atQuarterHour);

        Assert.Equal(new DateTime(2026, 7, 6, 12, 30, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void GetNextOccurrence_DayAnchoredCron_IsEvaluatedInUtc()
    {
        // Daily at 03:00 UTC; after 2026-07-06 12:00 UTC the next run should be 03:00 UTC the following day.
        var next = _sut.GetNextOccurrence("0 0 3 * * ?", _afterUtc);

        Assert.Equal(new DateTime(2026, 7, 7, 3, 0, 0, DateTimeKind.Utc), next);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetNextOccurrence_NullOrWhitespaceCron_ReturnsNull(string? cron)
    {
        Assert.Null(_sut.GetNextOccurrence(cron, _afterUtc));
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("* * * *")]
    public void GetNextOccurrence_InvalidCron_ThrowsBadRequest(string cron)
    {
        Assert.Throws<BadRequestException>(() => _sut.GetNextOccurrence(cron, _afterUtc));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSchedule_NullOrWhitespaceCron_IsValid(string? cron)
    {
        // Null means "no scheduled rotation" and is always accepted.
        _sut.ValidateSchedule(cron, TimeSpan.FromMinutes(15));
    }

    [Theory]
    [InlineData("not a cron")]
    [InlineData("* * * *")]
    public void ValidateSchedule_InvalidCron_ThrowsBadRequest(string cron)
    {
        Assert.Throws<BadRequestException>(() => _sut.ValidateSchedule(cron, TimeSpan.FromMinutes(15)));
    }

    [Fact]
    public void ValidateSchedule_SubFloorCadence_ThrowsBadRequest()
    {
        // Every minute is below the 15-minute floor.
        var ex = Assert.Throws<BadRequestException>(
            () => _sut.ValidateSchedule("0 * * * * ?", TimeSpan.FromMinutes(15)));

        Assert.Contains("15", ex.Message);
    }

    [Fact]
    public void ValidateSchedule_CadenceAtOrAboveFloor_IsValid()
    {
        // Daily at 03:00 clears a 15-minute floor easily.
        _sut.ValidateSchedule("0 0 3 * * ?", TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ValidateSchedule_CadenceExactlyAtFloor_IsValid()
    {
        // Every 15 minutes meets a 15-minute floor exactly (the guard is strictly-less-than).
        _sut.ValidateSchedule("0 0/15 * * * ?", TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ValidateSchedule_ScheduleThatNeverOccurs_ThrowsBadRequest()
    {
        // A 7-field Quartz cron pinned to a year in the past never fires again; the floor cannot be checked, so it
        // is rejected the same way a too-frequent schedule is.
        Assert.Throws<BadRequestException>(
            () => _sut.ValidateSchedule("0 0 0 1 1 ? 2001", TimeSpan.FromMinutes(15)));
    }
}
