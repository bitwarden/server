using System;
using Bit.Core.Models.Table;
using Bit.Core.Models.Business;
using Bit.Core.Enums;
using System.Collections.Generic;
using System.Linq;
using Fido2NetLib;

namespace Bit.Core.Models.Api
{
    public class TwoFactorWebAuthnResponseModel : ResponseModel
    {
        public TwoFactorWebAuthnResponseModel(User user)
            : base("twoFactorU2f")
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
            }

            public string Name { get; set; }
            public int Id { get; set; }
        }
    }
}
