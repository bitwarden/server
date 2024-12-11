﻿using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorEmailResponseModel : ResponseModel
{
    public TwoFactorEmailResponseModel(User user)
        : base("twoFactorEmail")
    {
        ArgumentNullException.ThrowIfNull(user);

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.Email);
        if (provider?.MetaData?.ContainsKey("Email") ?? false)
        {
            Email = (string)provider.MetaData["Email"];
            Enabled = provider.Enabled;
        }
        else
        {
            Enabled = false;
        }
    }

    public bool Enabled { get; set; }
    public string Email { get; set; }
}
