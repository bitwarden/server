using System.ComponentModel.DataAnnotations;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models;

public class SendDataModel
{
    public SendDataModel() { }

    public SendDataModel(SendItemData data)
    {
        EncryptionVersion = data.EncryptionVersion;
        Data = data.Data;
    }

    public SendEncryptionType EncryptionVersion { get; set; } = SendEncryptionType.V1;

    [StringLength(500000)]
    public string? Data { get; set; }
}
