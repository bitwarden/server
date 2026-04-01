using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class ReceiveFileUploadDataResponseModel(string url, string fileId, FileUploadType fileUploadType) : ResponseModel("receiveFileUpload")
{
    public string Url { get; } = url;
    public string FileId { get; } = fileId;
    public FileUploadType FileUploadType { get; set; } = fileUploadType;
}
