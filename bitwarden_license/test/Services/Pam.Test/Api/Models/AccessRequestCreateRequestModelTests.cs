using System.Text.Json;
using Bit.Services.Pam.Api.Models.Request;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

/// <summary>
/// The requested window is the only clock PAM takes off the wire, and it lands in a column every other part of the
/// subsystem reads as UTC. These cover the three shapes System.Text.Json can produce for a timestamp, so the stored
/// instant is the one the requester picked regardless of the API host's timezone.
/// </summary>
public class AccessRequestCreateRequestModelTests
{
    /// <summary>Minimal APIs bind with the Web defaults, so the tests parse the same way the endpoint does.</summary>
    private static readonly JsonSerializerOptions _webDefaults = new(JsonSerializerDefaults.Web);

    private static AccessRequestCreateRequestModel Deserialize(string start, string end) =>
        JsonSerializer.Deserialize<AccessRequestCreateRequestModel>(
            $$"""{"start":"{{start}}","end":"{{end}}","reason":"audit"}""", _webDefaults)!;

    /// <summary>
    /// The regression behind PM-42275. chrono's <c>to_rfc3339</c> — what the Rust SDK sends — writes a zero offset as
    /// <c>+00:00</c> rather than <c>Z</c>, and System.Text.Json resolves any explicit offset against the host's
    /// timezone, handing back a <see cref="DateTimeKind.Local"/> value. Persisted as-is, that shifted the window by
    /// the host's UTC offset. A non-zero offset is used here so the conversion is exercised on a UTC host too.
    /// </summary>
    [Theory]
    [InlineData("2026-06-15T13:00:00+00:00", "2026-06-15T14:00:00+00:00")]
    [InlineData("2026-06-15T15:00:00+02:00", "2026-06-15T16:00:00+02:00")]
    [InlineData("2026-06-15T08:00:00-05:00", "2026-06-15T09:00:00-05:00")]
    public void ToSubmission_WindowSentWithAnExplicitOffset_KeepsTheInstantTheRequesterPicked(string start, string end)
    {
        var submission = Deserialize(start, end).ToSubmission();

        Assert.Equal(new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Utc), submission.Start);
        Assert.Equal(new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc), submission.End);
        // DateTime equality ignores Kind, so the values above pass on a UTC host even unconverted. The kind is what
        // proves the conversion ran, and is what the DATETIME2 column's readers rely on.
        Assert.Equal(DateTimeKind.Utc, submission.Start!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, submission.End!.Value.Kind);
    }

    /// <summary>A <c>Z</c>-suffixed window — what every JavaScript client's <c>toISOString()</c> sends — is already
    /// the instant we want, and must survive untouched.</summary>
    [Fact]
    public void ToSubmission_WindowSentAsUtc_IsUnchanged()
    {
        var submission = Deserialize("2026-06-15T13:00:00Z", "2026-06-15T14:00:00Z").ToSubmission();

        Assert.Equal(new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Utc), submission.Start);
        Assert.Equal(new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc), submission.End);
        Assert.Equal(DateTimeKind.Utc, submission.Start!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, submission.End!.Value.Kind);
    }

    /// <summary>
    /// A timestamp carrying no designator is ambiguous on the wire; reading it as UTC keeps the stored instant
    /// independent of the host's timezone, rather than silently meaning something different per deployment.
    /// </summary>
    [Fact]
    public void ToSubmission_WindowSentWithoutADesignator_IsReadAsUtc()
    {
        var submission = Deserialize("2026-06-15T13:00:00", "2026-06-15T14:00:00").ToSubmission();

        Assert.Equal(new DateTime(2026, 6, 15, 13, 0, 0, DateTimeKind.Utc), submission.Start);
        Assert.Equal(new DateTime(2026, 6, 15, 14, 0, 0, DateTimeKind.Utc), submission.End);
        Assert.Equal(DateTimeKind.Utc, submission.Start!.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, submission.End!.Value.Kind);
    }

    /// <summary>The automatic path sends a duration and no window; normalising must not invent one.</summary>
    [Fact]
    public void ToSubmission_AutomaticPath_LeavesTheWindowUnset()
    {
        var model = JsonSerializer.Deserialize<AccessRequestCreateRequestModel>(
            """{"durationSeconds":3600,"reason":"audit"}""", _webDefaults)!;

        var submission = model.ToSubmission();

        Assert.Equal(3600, submission.DurationSeconds);
        Assert.Null(submission.Start);
        Assert.Null(submission.End);
        Assert.Equal("audit", submission.Reason);
    }
}
