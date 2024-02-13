using Bit.Core.Enums;

namespace Bit.Core.Vault.Models.Data;

public class CipherLoginData : CipherData
{
    private string _uri;

    public CipherLoginData() { }

    public string Uri
    {
        get => Uris?.FirstOrDefault()?.Uri ?? _uri;
        set { _uri = value; }
    }
    public IEnumerable<CipherLoginUriData> Uris { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public DateTime? PasswordRevisionDate { get; set; }
    public string Totp { get; set; }
    public bool? AutofillOnPageLoad { get; set; }
    public CipherLoginFido2CredentialData[] Fido2Credentials { get; set; }

    public class CipherLoginUriData
    {
        public CipherLoginUriData() { }

        public string Uri { get; set; }
        public string UriChecksum { get; set; }
        public UriMatchType? Match { get; set; } = null;
    }
}
