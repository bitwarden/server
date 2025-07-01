﻿using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using OtpNet;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorAuthenticatorResponseModel : ResponseModel
{
    public TwoFactorAuthenticatorResponseModel(User user)
        : base("twoFactorAuthenticator")
    {
        ArgumentNullException.ThrowIfNull(user);

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Authenticator);
        if (provider?.MetaData?.TryGetValue("Key", out var keyValue) ?? false)
        {
            Key = (string)keyValue;
            Enabled = provider.Enabled;
        }
        else
        {
            var key = KeyGeneration.GenerateRandomKey(20);
            Key = Base32Encoding.ToString(key);
            Enabled = false;
        }
    }

    public bool Enabled { get; set; }
    public string Key { get; set; }
    public string UserVerificationToken { get; set; }
}
