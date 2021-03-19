using Bit.Core.Enums;

namespace Bit.Core.Models.Api.Response
{
    public class SendFileUploadDataResponseModel : ResponseModel
    {
        public SendFileUploadDataResponseModel() : base("send-fileUpload") { }
        
        public string Url { get; set; }
        public FileUploadType FileUploadType { get; set; }
        public SendResponseModel SendResponse { get; set; }

    }
}
