namespace Bit.AppHost.Configuration;

/// <summary>
/// A container that publishes one host port.
/// </summary>
internal sealed record ContainerSettings : ContainerImageSettings
{
    /// <summary>
    /// Image reference in <c>name[:tag]</c> form; the key is required.
    /// </summary>
    internal override required string Image { get; init; }

    /// <summary>
    /// Host port the container publishes.
    /// </summary>
    internal required int Port { get; init; }
}
