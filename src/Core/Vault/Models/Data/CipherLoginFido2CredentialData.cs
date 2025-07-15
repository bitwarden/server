﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Vault.Models.Data;

public class CipherLoginFido2CredentialData
{
    public CipherLoginFido2CredentialData() { }

    public string CredentialId { get; set; }
    public string KeyType { get; set; }
    public string KeyAlgorithm { get; set; }
    public string KeyCurve { get; set; }
    public string KeyValue { get; set; }
    public string RpId { get; set; }
    public string RpName { get; set; }
    public string UserHandle { get; set; }
    public string UserName { get; set; }
    public string UserDisplayName { get; set; }
    public string Counter { get; set; }
    public string Discoverable { get; set; }
    public DateTime CreationDate { get; set; }
}
