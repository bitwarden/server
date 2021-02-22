namespace Bit.Core.Models.Api.Response
{
    public class SendFileDownloadDataResponseModel : ResponseModel
    {
        public string Id { get; set; }
        public string Url { get; set; }

        public SendFileDownloadDataResponseModel() : base("send-fileDownload") { }
    }
}
