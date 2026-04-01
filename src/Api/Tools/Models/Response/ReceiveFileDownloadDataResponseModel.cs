using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class ReceiveFileDownloadDataResponseModel : ResponseModel
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public ReceiveFileDownloadDataResponseModel() : base("receive-fileDownload") { }
}
