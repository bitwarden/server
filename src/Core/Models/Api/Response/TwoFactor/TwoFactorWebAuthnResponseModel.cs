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


            var provider = user.GetTwoFactorProvider(TwoFactorProviderType.U2f);
            Enabled = provider?.Enabled ?? false;
            Keys = provider?.MetaData?.Select(k => new KeyModel(k.Key,
                new TwoFactorProvider.U2fMetaData((dynamic)k.Value)));
        }

        public bool Enabled { get; set; }
        public IEnumerable<KeyModel> Keys { get; set; }

        public class KeyModel
        {
            public KeyModel(string id, TwoFactorProvider.U2fMetaData data)
            {
                Name = data.Name;
                Id = Convert.ToInt32(id.Replace("Key", string.Empty));
                Compromised = data.Compromised;
            }

            public string Name { get; set; }
            public int Id { get; set; }
            public bool Compromised { get; set; }
        }
    }
}
