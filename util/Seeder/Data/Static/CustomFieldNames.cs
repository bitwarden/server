namespace Bit.Seeder.Data.Static;

/// <summary>
/// The names that generated login custom fields draw from.
/// </summary>
internal static class CustomFieldNames
{
    /// <summary>
    /// Field names a login can carry. A login takes one to three of them, never the same name twice.
    /// </summary>
    internal static readonly string[] All = ["Account ID", "Tenant", "Customer number", "PIN", "Region", "API key"];
}
