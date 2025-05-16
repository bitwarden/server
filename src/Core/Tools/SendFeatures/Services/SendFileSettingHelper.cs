using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.SendFeatures;

/// <summary>
/// SendFileSettingHelper is a static class that provides constants and helper methods (if needed) for managing file
/// settings.
/// </summary>
public static class SendFileSettingHelper
{
    /// <summary>
    /// The leeway for the file size. This is the calculated 1 megabyte of cushion when doing comparisons of file sizes
    /// within the system.
    /// </summary>
    public const long FILE_SIZE_LEEWAY = 1024L * 1024L; // 1MB
    /// <summary>
    /// The maximum file size for a file uploaded in a <see cref="Send" />. Units are calculated in bytes but
    /// represent 501 megabytes. 1 megabyte is added for cushion to account for file size.
    /// </summary>
    public const long MAX_FILE_SIZE = Constants.FileSize501mb;

    /// <summary>
    /// String of the expected file size and to be used when needing to communicate the file size to the client/user.
    /// </summary>
    public const string MAX_FILE_SIZE_READABLE = "500 MB";
}
