namespace Bit.Commercial.Pam.Api.Models.Response;

/// <summary>
/// Marks PAM response timestamps as UTC for serialization.
///
/// PAM entities and read models are materialised by Dapper, which leaves their <see cref="DateTime.Kind"/> as
/// <see cref="DateTimeKind.Unspecified"/>. System.Text.Json then writes an unspecified-kind value with no timezone
/// designator (e.g. <c>"2026-06-15T13:00:00"</c>), which a JavaScript client parses as <em>local</em> time. For any
/// client east/west of UTC the instant shifts — and in the approver inbox that shift drops still-valid requests whose
/// requested window only appears to have lapsed.
///
/// The stored values are already UTC instants (the commands stamp them from <c>UtcNow</c>), so we relabel the kind
/// with <see cref="DateTime.SpecifyKind"/>. We deliberately do not use <c>ToUniversalTime()</c>, which treats an
/// unspecified value as local and would shift the clock. This mirrors the convention in <c>CipherRepository</c>, which
/// specifies UTC on the dates it returns.
/// </summary>
internal static class PamDateTimeExtensions
{
    public static DateTime AsUtc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtc(this DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
