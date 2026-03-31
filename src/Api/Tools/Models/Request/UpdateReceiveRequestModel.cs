using System.ComponentModel.DataAnnotations;
using Bit.Core.Tools.ReceiveFeatures.Models;
using Bit.Core.Utilities;


namespace Bit.Api.Tools.Models.Request;

public class UpdateReceiveRequestModel
{
    /// <summary>
    /// Label for the Receive.
    /// </summary>
    [Required]
    [EncryptedString]
    [EncryptedStringLength(1000)]
    public required string Name { get; set; }

    /// <summary>
    /// The date this Receive becomes unavailable to potential uploaders.
    /// </summary>
    public DateTime ExpirationDate { get; set; }

    public ReceiveUpdateData ToUpdateData(Guid id)
    {
        return new ReceiveUpdateData { Id = id, Name = Name, ExpirationDate = ExpirationDate, };
    }
}
