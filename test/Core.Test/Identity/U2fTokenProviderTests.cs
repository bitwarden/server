using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Identity;
using Bit.Core.Models.Table;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Identity
{
    public class U2fTokenProviderTests : BaseTokenProviderTests<U2fTokenProvider>
    {
        public override TwoFactorProviderType TwoFactorProviderType => TwoFactorProviderType.U2f;

        public static IEnumerable<object[]> CanGenerateTwoFactorTokenAsyncData()
        {
            return new[]
            {
                new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["Something"] = "Hello"
                    },
                    true, // canAccessPremium
                    true, // expectedResponse
                },
                new object[]
                {
                    new Dictionary<string, object>(),
                    true, // canAccessPremium
                    false, // expectedResponse
                },
                new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["Key"] = "Value"
                    },
                    false, // canAccessPremium
                    false, // expectedResponse
                },
            };
        }

        [Theory, BitMemberAutoData(nameof(CanGenerateTwoFactorTokenAsyncData))]
        public async Task CanGenerateTwoFactorTokenAsync_Success(Dictionary<string, object> metaData, bool canAccessPremium,
            bool expectedResponse, User user, SutProvider<U2fTokenProvider> sutProvider)
        {
            var userManager = SubstituteUserManager();
            MockDatabase(user, metaData);
            AdditionalSetup(sutProvider, user)
                .CanAccessPremium(user)
                .Returns(canAccessPremium);

            var response = await sutProvider.Sut.CanGenerateTwoFactorTokenAsync(userManager, user);
            Assert.Equal(expectedResponse, response);
        }
    }
}
