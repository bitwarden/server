namespace Bit.AppHost.Configuration;

/// <summary>
/// A container started from a <c>name[:tag]</c> image reference.
/// </summary>
internal abstract record ContainerImageSettings
{
    /// <summary>
    /// Image reference in <c>name[:tag]</c> form. Each subtype decides whether its key is required.
    /// </summary>
    internal abstract string Image { get; init; }

    /// <summary>
    /// Splits <see cref="Image"/> into name and tag, defaulting the tag to <c>latest</c>.
    /// </summary>
    internal (string Name, string Tag) GetImageParts()
    {
        var parts = Image.Split(':');
        return (parts[0], parts.Length > 1 ? parts[1] : "latest");
    }
}
