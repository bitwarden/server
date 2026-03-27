using System.Text.Json;
using Bit.Core.Models.Api;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models.Response;

public class SharedReceiveResponseModel : ResponseModel
{
    public SharedReceiveResponseModel(Receive receive) : base("receiveShared")
    {
        var fileData = JsonSerializer.Deserialize<ReceiveFileData>(receive.Data);
        Name = fileData!.Name;
        ScekWrappedPublicKey = receive.ScekWrappedPublicKey;
    }

    /// <summary>Label for the Receive. Encrypted.</summary>
    public string Name { get; set; }

    /// <summary>Public key (SCEK-wrapped) used by uploaders to encrypt file content.</summary>
    public string ScekWrappedPublicKey { get; set; }
}
