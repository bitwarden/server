#nullable enable

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// A text secret being sent.
/// </summary>
public class SendTextData : SendData
{
    /// <summary>
    /// Instantiates a <see cref="SendTextData"/>.
    /// </summary>
    public SendTextData() { }

    /// <inheritdoc cref="SendTextData()"/>
    /// <param name="name">Attached file name.</param>
    /// <param name="notes">User-provided private notes of the send.</param>
    /// <param name="text">The secret being sent.</param>
    /// <param name="hidden">
    /// Indicates whether the secret should be concealed when opening the send.
    /// </param>
    public SendTextData(string name, string? notes, string? text, bool hidden)
        : base(name, notes)
    {
        Text = text;
        Hidden = hidden;
    }

    /// <summary>
    /// The secret being sent.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Indicates whether the secret should be concealed when opening the send.
    /// </summary>
    /// <value>
    /// <see langword="true" /> when the secret should be concealed.
    /// Otherwise <see langword="false" />.
    /// </value>
    public bool Hidden { get; set; }
}
