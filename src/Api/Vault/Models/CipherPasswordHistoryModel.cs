﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

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
