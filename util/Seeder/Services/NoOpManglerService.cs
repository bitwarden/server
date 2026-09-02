namespace Bit.Seeder.Services;

/// <summary>
/// No-op implementation for dev/human-readable mode.
/// Returns original values unchanged. Registered as singleton since it has no state.
/// </summary>
public class NoOpManglerService : IManglerService
{
    public string Mangle(string value) => value;

    public Dictionary<string, string?> GetMangleMap() => new();

    public bool IsEnabled => false;
}
