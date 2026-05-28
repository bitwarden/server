using System.Security.Cryptography;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Infrastructure.Dapper.Tools.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bit.Infrastructure.Dapper.Test.Tools;

public class SendRepositoryTests
{
    [Fact]
    public async Task CreateAsync_WhenProtectFails_ThrowsBeforePersisting()
    {
        // ProtectDataAndSaveAsync must throw before invoking saveTask() so that a
        // Data Protection failure cannot result in plaintext Emails reaching the
        // [dbo].[Send].[Emails] column. Dummy connection strings are safe here
        // because the throw happens before any SqlConnection is opened.
        var repository = new SendRepository(
            connectionString: "unused-but-non-empty",
            readOnlyConnectionString: "unused-but-non-empty",
            dataProtectionProvider: new ThrowingDataProtectionProvider(),
            logger: NullLogger<SendRepository>.Instance);

        var send = new Send
        {
            Type = SendType.Text,
            Data = "{\"Text\":\"2.t|t|t\"}",
            Key = "2.t|t|t",
            Emails = "user@example.com",
            DeletionDate = DateTime.UtcNow.AddDays(7),
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.CreateAsync(send));
        Assert.Contains("Emails could not be protected", exception.Message);
    }

    private sealed class ThrowingDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => new ThrowingDataProtector();
    }

    private sealed class ThrowingDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) =>
            throw new CryptographicException("Test-induced Protect failure");

        public byte[] Unprotect(byte[] protectedData) =>
            throw new CryptographicException("Test-induced Unprotect failure");
    }
}
