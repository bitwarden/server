using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Pam.Services;
using Bit.Core.Settings;
using Bit.Core.Vault.Authorization;
using Bit.Core.Vault.Entities;
using Bit.SharedWeb.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SharedWeb.Test.Utilities;

/// <summary>
/// Pins the default <see cref="ICipherLeaseGate"/> registration. PAM leasing is wired end to end, but nothing is
/// gated: the default gate lets every cipher read through. These tests exist so that stays a decision rather than an
/// accident — when the commercial gate lands and replaces the default, the first test fails and points at the change.
/// </summary>
public class CipherLeaseGateRegistrationTests
{
    [Fact]
    public void AddBaseServices_RegistersUnrestrictedGate_SoLeasingIsNotEnforced()
    {
        var services = new ServiceCollection();

        services.AddBaseServices(new GlobalSettings());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICipherLeaseGate));
        Assert.Equal(typeof(UnrestrictedCipherLeaseGate), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBaseServices_RegistersGateSoALaterPlainAddWins()
    {
        // The commercial gate overrides the default by registering after AddBaseServices and relying on last-one-wins.
        // That only holds while the default is a plain Add: a TryAdd default would still be the sole registration, and
        // a TryAdd override would silently no-op and leave leasing ungated.
        var services = new ServiceCollection();
        services.AddBaseServices(new GlobalSettings());

        services.AddScoped<ICipherLeaseGate, StubCipherLeaseGate>();

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();
        Assert.IsType<StubCipherLeaseGate>(scope.ServiceProvider.GetRequiredService<ICipherLeaseGate>());
    }

    /// <summary>Stands in for the commercial gate. Only its type is asserted; no method is ever called.</summary>
    private sealed class StubCipherLeaseGate : ICipherLeaseGate
    {
        public Task<FullCipherAccess?> AuthorizeReadAsync(Guid userId, Cipher cipher) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess> AuthorizeReadManyAsync(
            Guid userId,
            IEnumerable<Cipher> ciphers,
            IEnumerable<CollectionDetails>? collections,
            IDictionary<Guid, IGrouping<Guid, CollectionCipher>>? collectionCiphersByCipher) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess> AuthorizeReadManyAsync(Guid userId, IEnumerable<Cipher> ciphers) =>
            throw new NotSupportedException();

        public FullCipherAccess Unrestricted() => throw new NotSupportedException();
    }
}
