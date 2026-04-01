using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class ReceiveFileDownloadDataResponseModel(string fileId, string url) : ResponseModel("receiveFileDownload")
{
    public string Id { get; } = fileId;
    public string Url { get; } = url;
}
