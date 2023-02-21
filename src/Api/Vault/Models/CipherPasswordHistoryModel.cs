using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Models;

public class CipherPasswordHistoryModel
{
    public CipherPasswordHistoryModel() { }

    public CipherPasswordHistoryModel(CipherPasswordHistoryData data)
    {
        Password = data.Password;
        LastUsedDate = data.LastUsedDate;
    }

    [EncryptedString]
    [EncryptedStringLength(5000)]
    [Required]
    public string Password { get; set; }
    [Required]
    public DateTime? LastUsedDate { get; set; }

    public CipherPasswordHistoryData ToCipherPasswordHistoryData()
    {
        return new CipherPasswordHistoryData { Password = Password, LastUsedDate = LastUsedDate.Value, };
    }
}
