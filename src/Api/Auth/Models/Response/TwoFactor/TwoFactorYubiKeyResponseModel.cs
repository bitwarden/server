using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorYubiKeyResponseModel : ResponseModel
{
    public TwoFactorYubiKeyResponseModel(User user)
        : base("twoFactorYubiKey")
    {
        ArgumentNullException.ThrowIfNull(user);

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.YubiKey);
        if (provider?.MetaData != null && provider.MetaData.Count > 0)
        {
            Enabled = provider.Enabled;

            if (provider.MetaData.TryGetValue("Key1", out var value))
            {
                Key1 = (string)value;
            }
            if (provider.MetaData.TryGetValue("Key2", out var value))
            {
                Key2 = (string)value;
            }
            if (provider.MetaData.TryGetValue("Key3", out var value))
            {
                Key3 = (string)value;
            }
            if (provider.MetaData.TryGetValue("Key4", out var value))
            {
                Key4 = (string)value;
            }
            if (provider.MetaData.TryGetValue("Key5", out var value))
            {
                Key5 = (string)value;
            }
            if (provider.MetaData.TryGetValue("Nfc", out var value))
            {
                Nfc = (bool)value;
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
