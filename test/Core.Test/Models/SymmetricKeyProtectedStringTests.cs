using System;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Models.Business
{
    public class SymmetricKeyProtectedStringTests
    {
        [Theory]
        [BitAutoData]
        public void Encryption_EncryptionRoundTrip_Success(string clearText, Guid password)
        {
            var protString = SymmetricKeyProtectedString.Encrypt(clearText, password.ToString());
            var decryptedString = protString.Decrypt(password.ToString());

            Assert.Equal(clearText, decryptedString);
        }

        [Theory]
        [BitAutoData]
        public void Encryption_B64StringRoundTrip_Success(string clearText, Guid password)
        {
            var protString = SymmetricKeyProtectedString.Encrypt(clearText, password.ToString());
            var parseProtString = new SymmetricKeyProtectedString(protString.EncryptedString);
            var decryptedString = parseProtString.Decrypt(password.ToString());

            Assert.Equal(clearText, decryptedString);
        }

        [Theory]
        [BitAutoData]
        public void NullWithWrongKey(string clearText, string key1, string key2)
        {
            var protString = SymmetricKeyProtectedString.Encrypt(clearText, key1);
            var result = protString.Decrypt(key2);

            Assert.Null(result);
        }
    }
}
