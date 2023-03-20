using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response.TwoFactor;

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

            if (provider.MetaData.ContainsKey("Key1"))
            {
                Key1 = (string)provider.MetaData["Key1"];
            }
            if (provider.MetaData.ContainsKey("Key2"))
            {
                Key2 = (string)provider.MetaData["Key2"];
            }
            if (provider.MetaData.ContainsKey("Key3"))
            {
                Key3 = (string)provider.MetaData["Key3"];
            }
            if (provider.MetaData.ContainsKey("Key4"))
            {
                Key4 = (string)provider.MetaData["Key4"];
            }
            if (provider.MetaData.ContainsKey("Key5"))
            {
                Key5 = (string)provider.MetaData["Key5"];
            }
            if (provider.MetaData.ContainsKey("Nfc"))
            {
                Nfc = (bool)provider.MetaData["Nfc"];
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
