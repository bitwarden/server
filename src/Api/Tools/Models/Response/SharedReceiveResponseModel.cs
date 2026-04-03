using Bit.Core.Models.Api;
using Bit.Core.Tools.Entities;

namespace Bit.Api.Tools.Models.Response;

public class SharedReceiveResponseModel : ResponseModel
{
    public SharedReceiveResponseModel(Receive receive, string email) : base("receiveShared")
    {
        Name = receive.Name;
        ScekWrappedPublicKey = receive.ScekWrappedPublicKey;
        OwnerEmail = email;
    }

    /// <summary>Label for the Receive. Encrypted.</summary>
    public string Name { get; set; }

    /// <summary>Public key (SCEK-wrapped) used by uploaders to encrypt file content.</summary>
    public string ScekWrappedPublicKey { get; set; }

    /// <summary>
    /// The Receive owner's email
    /// </summary>
    public string OwnerEmail { get; set; }
}
