namespace Bit.Seeder.Scenes;

internal static class ProviderSceneHelpers
{
    /// <summary>
    /// Resolves the domain used to derive the provider's non-deliverable billing email. The provider scene
    /// treats domain as optional (the postman contract omits it), so fall back to a unique test domain when
    /// none is supplied.
    /// </summary>
    internal static string ResolveDomain(string? domain) =>
        string.IsNullOrWhiteSpace(domain) ? $"{Guid.NewGuid():N}.provider.test" : domain;
}
