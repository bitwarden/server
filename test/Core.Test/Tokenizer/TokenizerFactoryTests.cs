using System;
using System.Collections.Generic;
using System.Linq;
using Bit.Core.Tokenizer;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Tokenizer
{
    [SutProviderCustomize]
    public class TokenizerFactoryTests
    {
        public static IEnumerable<object[]> TokenTypes =>
            Enum.GetValues<TokenType>().Select(p => new object[] { p });

        [Theory]
        [BitMemberAutoData(nameof(TokenTypes))]
        public void Create_PassesOnClearTextPrefix(TokenType tokenType, string clearTextPrefix, string key,
            SutProvider<TokenizerFactory> sutProvider)
        {
            var tokenizer = sutProvider.Sut.Create<TestToken>(clearTextPrefix, tokenType);

            var token = tokenizer.Protect(key, new TestToken());

            Assert.StartsWith(clearTextPrefix, token);
        }
    }
}
