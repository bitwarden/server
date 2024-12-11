using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class SendFileDownloadDataResponseModel : ResponseModel
{
    public string Id { get; set; }
    public string Url { get; set; }

    public SendFileDownloadDataResponseModel()
        : base("send-fileDownload") { }
}
