using Bit.Core.Tools.Services;

namespace Bit.Core.Tools.SendFeatures;

public class SendFileSettingHelper : ISendFileSettingHelper
{
    private readonly long _fileSizeLeeway = 1024L * 1024L; // 1MB
    private readonly long MAX_FILE_SIZE = Constants.FileSize501mb;
    private readonly string MAX_FILE_SIZE_READABLE = "500 MB";

    public long GetMaxFileSize()
    {
        return MAX_FILE_SIZE;
    }

    public string GetMaxFileSizeReadable()
    {
        return MAX_FILE_SIZE_READABLE;
    }

    public long GetFileSizeLeeway()
    {
        return _fileSizeLeeway;
    }
}
