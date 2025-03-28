﻿using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Data;

// TODO implement
public class OpaqueKeyExchangeRotateKeyData
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }

}
