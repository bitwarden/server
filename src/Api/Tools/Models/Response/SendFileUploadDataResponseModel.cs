using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class SendFileUploadDataResponseModel : ResponseModel
{
    public SendFileUploadDataResponseModel()
        : base("send-fileUpload") { }

    public string Url { get; set; }
    public FileUploadType FileUploadType { get; set; }
    public SendResponseModel SendResponse { get; set; }
}
