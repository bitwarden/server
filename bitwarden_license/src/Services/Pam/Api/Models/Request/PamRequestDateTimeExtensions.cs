namespace Bit.Services.Pam.Api.Models.Request;

/// <summary>
/// Normalises a client-supplied timestamp to a UTC <see cref="DateTime"/>. System.Text.Json parses an explicit
/// offset as <see cref="DateTimeKind.Local"/>, which is converted back to the instant it names; an
/// <see cref="DateTimeKind.Unspecified"/> value is read as UTC.
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
