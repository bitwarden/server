namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// Marks Dapper's <see cref="DateTimeKind.Unspecified"/> timestamps as UTC without converting them, since the stored
/// values are UTC. Otherwise System.Text.Json writes them without a zone and JavaScript parses them as local time.
/// </summary>
internal static class PamDateTimeExtensions
{
    public static DateTime AsUtc(this DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? AsUtc(this DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
