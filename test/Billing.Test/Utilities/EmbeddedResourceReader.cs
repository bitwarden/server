using System.Reflection;

namespace Bit.Billing.Test.Utilities;

public static class EmbeddedResourceReader
{
    public static async Task<string> ReadAsync(string resourceType, string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        await using var stream = assembly.GetManifestResourceStream($"Bit.Billing.Test.Resources.{resourceType}.{fileName}");

        if (stream == null)
        {
            throw new Exception($"Failed to retrieve manifest resource stream for file: {fileName}.");
        }

        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
}
