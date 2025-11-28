// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class SendFileDownloadDataResponseModel : ResponseModel
{
    public string Id { get; set; }
    public string Url { get; set; }

    public SendFileDownloadDataResponseModel() : base("send-fileDownload") { }
}
