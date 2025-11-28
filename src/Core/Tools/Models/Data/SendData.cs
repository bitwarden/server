#nullable enable

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// Shared data for a send
/// </summary>
public abstract class SendData
{
    /// <summary>
    /// Instantiates a <see cref="SendData"/>.
    /// </summary>
    public SendData() { }

    /// <inheritdoc cref="SendData()" />
    /// <param name="name">User-provided name of the send.</param>
    /// <param name="notes">User-provided private notes of the send.</param>
    public SendData(string name, string? notes)
    {
        Name = name;
        Notes = notes;
    }

    /// <summary>
    /// User-provided name of the send.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// User-provided private notes of the send.
    /// </summary>
    public string? Notes { get; set; } = null;
}
