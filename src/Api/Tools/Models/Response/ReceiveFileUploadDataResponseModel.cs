using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

/// <summary>
/// Returned to anonymous uploaders after requesting a file upload slot.
/// Properties are serialized to JSON for the client; they have no server-side readers.
/// </summary>
public class ReceiveFileUploadDataResponseModel(string url, string fileId, FileUploadType fileUploadType) : ResponseModel("receiveFileUpload")
{
    /// <summary>
    /// URL the client uploads the encrypted file to.
    /// </summary>
    public string Url { get; } = url;

    /// <summary>
    /// Server-generated identifier for this file. The client must include it
    /// when calling the validation endpoint (POST /receives/{id}/file/{fileId}/validate).
    /// </summary>
    public string FileId { get; } = fileId;

    /// <summary>
    /// Tells the client which upload strategy to use (Azure SAS vs direct).
    /// </summary>
    public FileUploadType FileUploadType { get; set; } = fileUploadType;
}
