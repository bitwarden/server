using Bit.Core.Enums;

namespace Bit.Core.Models.Data;

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

    public class CipherLoginUriData
    {
        public CipherLoginUriData() { }

        public string Uri { get; set; }
        public UriMatchType? Match { get; set; } = null;
    }
}
