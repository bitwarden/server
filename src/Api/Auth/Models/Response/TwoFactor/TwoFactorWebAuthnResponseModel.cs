﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorWebAuthnResponseModel : ResponseModel
{
    public TwoFactorWebAuthnResponseModel(User user)
        : base("twoFactorWebAuthn")
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        var provider = user.GetTwoFactorProvider(TwoFactorProviderType.WebAuthn);
        Enabled = provider?.Enabled ?? false;
        Keys = provider?.MetaData?
            .Where(k => k.Key.StartsWith("Key"))
            .Select(k => new KeyModel(k.Key, new TwoFactorProvider.WebAuthnData((dynamic)k.Value)));
    }

    public bool Enabled { get; set; }
    public IEnumerable<KeyModel> Keys { get; set; }

    public class KeyModel
    {
        public KeyModel(string id, TwoFactorProvider.WebAuthnData data)
        {
            Name = data.Name;
            Id = Convert.ToInt32(id.Replace("Key", string.Empty));
            Migrated = data.Migrated;
        }

        public string Name { get; set; }
        public int Id { get; set; }
        public bool Migrated { get; set; }
    }
}
