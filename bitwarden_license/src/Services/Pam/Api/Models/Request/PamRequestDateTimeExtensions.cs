namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// Normalises a client-supplied timestamp onto a UTC <see cref="DateTime"/> before it reaches the domain.
///
/// PAM keeps every timestamp as a UTC instant in a <c>DATETIME2</c> column, and the commands stamp their own from
/// <c>UtcNow</c>. A requester-supplied access window is the one clock PAM takes off the wire, and what
/// System.Text.Json hands back depends on how the caller spelled it:
///
/// <list type="bullet">
///   <item><c>"2026-06-15T13:00:00Z"</c> — <see cref="DateTimeKind.Utc"/>, already the instant we want.</item>
///   <item><c>"2026-06-15T13:00:00+00:00"</c> — any explicit offset (including a zero one, which is what chrono's
///     <c>to_rfc3339</c> emits) is resolved against the <em>host's</em> timezone and returned as
///     <see cref="DateTimeKind.Local"/>. Persisted as-is, the window then shifts by the host's UTC offset.</item>
///   <item><c>"2026-06-15T13:00:00"</c> — no designator at all, so <see cref="DateTimeKind.Unspecified"/>.</item>
/// </list>
///
/// A local-kind value is therefore converted back to the instant it names — <see cref="DateTime.ToUniversalTime"/>
/// undoes exactly the shift the serializer applied, whatever the host's timezone — and an unspecified one is read as
/// UTC, matching what every Bitwarden client sends and what
/// <see cref="Bit.Services.Pam.Api.Models.Response.PamDateTimeExtensions"/> assumes on the way back out. The result is
/// the same stored instant on a UTC host and on one that is not.
///
/// This is the mirror of that response-side helper rather than a copy of it: reading relabels a kind Dapper left
/// blank, writing converts a kind the serializer chose. Hence <c>ToUtc</c> here against its <c>AsUtc</c> — one moves
/// the clock, the other only the label.
/// </summary>
internal static class PamRequestDateTimeExtensions
{
    public static DateTime ToUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? ToUtc(this DateTime? value) => value.HasValue ? value.Value.ToUtc() : null;
}
