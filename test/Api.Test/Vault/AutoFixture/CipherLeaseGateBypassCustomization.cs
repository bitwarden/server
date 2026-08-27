using AutoFixture;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;

namespace Bit.Api.Test.Vault.AutoFixture;

/// <summary>
/// Injects an <see cref="ICipherLeaseGate"/> substitute pre-configured to authorize full data for every
/// cipher — the flag-off / not-gated behaviour. This lets leasing-agnostic controller tests assert their
/// existing full-data expectations without each one having to stub the gate. Tests that exercise gating
/// re-stub the dependency (e.g. make <c>AuthorizeReadAsync</c> or <c>AuthorizeWriteReturnAsync</c> return
/// null) after building the SUT.
/// </summary>
public class CipherLeaseGateBypassCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var gate = Substitute.For<ICipherLeaseGate>();
        var unrestricted = FullCipherAccess.Unrestricted();

        gate.UnrestrictedForWholeVaultExport().Returns(unrestricted);
        gate.AuthorizeReadAsync(Arg.Any<Guid>(), Arg.Any<Cipher>()).Returns(unrestricted);
        gate.AuthorizeReadManyAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Cipher>>()).Returns(unrestricted);
        gate.AuthorizeReadManyAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Cipher>>(),
                Arg.Any<IEnumerable<CollectionDetails>>(),
                Arg.Any<IDictionary<Guid, IGrouping<Guid, CollectionCipher>>>())
            .Returns(unrestricted);
        gate.AuthorizeAdminReadAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Cipher>()).Returns(unrestricted);
        gate.AuthorizeAdminReadManyAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IEnumerable<Cipher>>())
            .Returns(unrestricted);
        gate.AuthorizeWriteReturnAsync(Arg.Any<Guid>(), Arg.Any<Cipher>()).Returns(unrestricted);
        gate.AuthorizeAdminWriteReturnAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Cipher>())
            .Returns(unrestricted);

        fixture.Inject(gate);
    }
}

public class CipherLeaseGateBypassCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new CipherLeaseGateBypassCustomization();
}
