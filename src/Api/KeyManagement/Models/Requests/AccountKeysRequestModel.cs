﻿#nullable enable
namespace Bit.Api.KeyManagement.Models.Requests;

public class AccountKeysRequestModel
{
    [EncryptedString] public required string UserKeyEncryptedAccountPrivateKey { get; set; }
    public required string AccountPublicKey { get; set; }
}
