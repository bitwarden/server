namespace Bit.Core.Tools.Services;

public interface ISendFileSettingHelper
{
    /// <summary>
    /// Gets the maximum file size for a Send.
    /// </summary>
    /// <returns>Max file size for a <see cref="Send" /> </returns>
    long GetMaxFileSize();

    /// <summary>
    /// Gets the maximum file size for a Send in a human-readable format.
    /// </summary>
    /// <returns>Max file size for a <see cref="Send" />  in human-readable format</returns>
    string GetMaxFileSizeReadable();

    /// <summary>
    /// Gets the leeway file size for a Send.
    /// </summary>
    /// <returns>Leeway file size for a <see cref="Send" /> </returns>
    long GetFileSizeLeeway();
}
