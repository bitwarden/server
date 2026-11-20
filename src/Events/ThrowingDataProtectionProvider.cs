using Microsoft.AspNetCore.DataProtection;

namespace Bit.Events;

/// <summary>
/// Satisfies dependencies on <see cref="IDataProtectionProvider"/> without registering Data Protection. Events never
/// protects or unprotects data, but shared repositories and the Entity Framework model create protectors eagerly.
/// Creating a protector succeeds, using one throws.
/// </summary>
/// <remarks>
/// <para>
/// Not registering Data Protection keeps the key ring from being loaded or created at startup, which otherwise logs a
/// false warning in self-hosted containers that don't mount the Data Protection key directory.
/// </para>
/// <para>
/// If this starts throwing because Events now reads or writes data that needs protecting or unprotecting, remove it and
/// register Data Protection with <c>AddCustomDataProtectionServices</c> again. The self-hosted Events container then
/// also needs the shared keys in <c>/etc/bitwarden/core/aspnet-dataprotection</c>, by mounting <c>../core</c> to
/// <c>/etc/bitwarden/core</c> like the other services that use Data Protection.
/// </para>
/// </remarks>
internal sealed class ThrowingDataProtectionProvider : IDataProtectionProvider
{
    public IDataProtector CreateProtector(string purpose) => ThrowingDataProtector.Instance;

    private sealed class ThrowingDataProtector : IDataProtector
    {
        public static readonly ThrowingDataProtector Instance = new();

        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => throw CreateException();

        public byte[] Unprotect(byte[] protectedData) => throw CreateException();

        private static InvalidOperationException CreateException() =>
            new("Data Protection is not supported in the Events service.");
    }
}
