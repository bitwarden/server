using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherCardModel
{
    public CipherCardModel() { }

    public CipherCardModel(CipherCardData data)
    {
        CardholderName = data.CardholderName;
        Brand = data.Brand;
        Number = data.Number;
        ExpMonth = data.ExpMonth;
        ExpYear = data.ExpYear;
        Code = data.Code;
    }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string CardholderName { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Brand { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Number { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string ExpMonth { get; set; }

    [EncryptedString]
    [StringLength(1000)]
    public string ExpYear { get; set; }

    [EncryptedString]
    [EncryptedStringLength(1000)]
    public string Code { get; set; }
}
