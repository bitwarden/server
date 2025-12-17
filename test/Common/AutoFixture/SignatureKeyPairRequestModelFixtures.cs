using AutoFixture;
using Bit.Core.KeyManagement.Models.Api.Request;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Test.Common.AutoFixture;

internal class SignatureKeyPairRequestModelCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<SignatureKeyPairRequestModel>(composer => composer
            .With(o => o.SignatureAlgorithm, "ed25519"));
    }
}

public class SignatureKeyPairRequestModelCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new SignatureKeyPairRequestModelCustomization();
}