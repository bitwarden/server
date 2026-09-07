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
/// Pins the <em>default</em> <see cref="ICipherLeaseGate"/> registration, the open-source fallback that lets every
/// cipher read and mutation through. The real gate is registered by <c>AddPamServices</c>, only in a non-OSS build.
/// </summary>
/// <remarks>
/// Relies on last-one-wins, so both registrations must stay a plain <c>Add</c>; the commercial half is pinned by
/// <c>ServiceCollectionExtensionsTests</c> in the Pam test project. A <c>TryAdd</c> on either side would leave
/// leasing silently ungated.
/// </remarks>
public class CipherLeaseGateRegistrationTests
{
    [Fact]
    public void AddBaseServices_RegistersUnrestrictedGate_AsTheOpenSourceDefault()
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
        // Stands in for what AddPamServices does; this project cannot reference the commercial library.
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

        public Task<FullCipherAccess?> AuthorizeWriteReturnAsync(Guid userId, Cipher cipher) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess?> AuthorizeAdminWriteReturnAsync(
            Guid userId, Guid organizationId, Cipher cipher) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess> EnsureCanMutateAsync(Guid userId, Cipher cipher) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess> EnsureCanMutateManyAsync(Guid userId, IEnumerable<Cipher> ciphers) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess?> AuthorizeAdminReadAsync(Guid userId, Guid organizationId, Cipher cipher) =>
            throw new NotSupportedException();

        public Task<FullCipherAccess> AuthorizeAdminReadManyAsync(
            Guid userId,
            Guid organizationId,
            IEnumerable<Cipher> ciphers) =>
            throw new NotSupportedException();

        public FullCipherAccess UnrestrictedForWholeVaultExport() => throw new NotSupportedException();
    }
}
