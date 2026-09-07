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
/// Injects an <see cref="ICipherLeaseGate"/> substitute authorizing full data and every mutation, the
/// flag-off/not-gated behavior; leasing-agnostic tests need not stub the gate themselves.
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
        gate.EnsureCanMutateAsync(Arg.Any<Guid>(), Arg.Any<Cipher>()).Returns(unrestricted);
        gate.EnsureCanMutateManyAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Cipher>>()).Returns(unrestricted);

        fixture.Inject(gate);
    }
}

public class CipherLeaseGateBypassCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new CipherLeaseGateBypassCustomization();
}
