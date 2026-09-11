namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// Normalises a client-supplied timestamp onto a UTC <see cref="DateTime"/> before it reaches the domain.
///
/// What System.Text.Json hands back depends on how the caller spelled the timestamp: a <c>Z</c> suffix parses as
/// <see cref="DateTimeKind.Utc"/>, an explicit offset (including a zero one) resolves against the host's
/// timezone as <see cref="DateTimeKind.Local"/>, and no designator at all is <see cref="DateTimeKind.Unspecified"/>.
///
/// A local-kind value is converted back to the instant it names via <see cref="DateTime.ToUniversalTime"/>; an
/// unspecified one is read as UTC, matching what every Bitwarden client sends. This mirrors the response-side
/// <see cref="Bit.Services.Pam.Api.Models.Response.PamDateTimeExtensions"/>: reading relabels a kind Dapper left
/// blank, writing converts a kind the serializer chose.
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
