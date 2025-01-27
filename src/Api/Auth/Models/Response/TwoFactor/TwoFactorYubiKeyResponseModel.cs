using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorYubiKeyResponseModel : ResponseModel
{
    public TwoFactorYubiKeyResponseModel(User user)
        : base("twoFactorYubiKey")
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.TryGetValue("Key1", out var key1))
            {
                Key1 = (string)key1;
            }
            if (provider.MetaData.TryGetValue("Key2", out var key2))
            {
                Key2 = (string)key2;
            }
            if (provider.MetaData.TryGetValue("Key3", out var key3))
            {
                Key3 = (string)key3;
            }
            if (provider.MetaData.TryGetValue("Key4", out var key4))
            {
                Key4 = (string)key4;
            }
            if (provider.MetaData.TryGetValue("Key5", out var key5))
            {
                Key5 = (string)key5;
            }
            if (provider.MetaData.TryGetValue("Nfc", out var nfc))
            {
                Nfc = (bool)nfc;
            }
        }
        else
        {
            Enabled = false;
        }
    }

    public bool Enabled { get; set; }
    public string Key1 { get; set; }
    public string Key2 { get; set; }
    public string Key3 { get; set; }
    public string Key4 { get; set; }
    public string Key5 { get; set; }
    public bool Nfc { get; set; }
}
