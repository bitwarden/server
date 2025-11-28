using System.Text;
using AutoFixture;
using Bit.Core.Settings;
using Bit.Core.Test.AutoFixture.CipherAttachmentMetaData;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class LocalAttachmentStorageServiceTests
{

    private void AssertFileCreation(string expectedPath, string expectedFileContents)
    {
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(expectedFileContents, File.ReadAllText(expectedPath));
    }

    [Theory]
    [InlineCustomAutoData(new[] { typeof(UserCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(UserCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(UserCipher), typeof(MetaDataWithoutKey) })]
    public async Task UploadNewAttachmentAsync_Success(string stream, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            await sutProvider.Sut.UploadNewAttachmentAsync(new MemoryStream(Encoding.UTF8.GetBytes(stream)),
                cipher, attachmentData);

            AssertFileCreation($"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}", stream);
        }
    }

    [Theory]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutKey) })]
    public async Task UploadShareAttachmentAsync_Success(string stream, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            await sutProvider.Sut.UploadShareAttachmentAsync(new MemoryStream(Encoding.UTF8.GetBytes(stream)),
                cipher.Id, cipher.OrganizationId.Value, attachmentData);

            AssertFileCreation($"{tempDirectory}/temp/{cipher.Id}/{cipher.OrganizationId}/{attachmentData.AttachmentId}", stream);
        }
    }

    [Theory]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutKey) })]
    public async Task StartShareAttachmentAsync_NoSource_NoWork(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            await sutProvider.Sut.StartShareAttachmentAsync(cipher.Id, cipher.OrganizationId.Value, attachmentData);

            Assert.False(File.Exists($"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}"));
            Assert.False(File.Exists($"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}"));
        }
    }

    [Theory]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutKey) })]
    public async Task StartShareAttachmentAsync_NoDest_NoWork(string source, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            var sourcePath = $"{tempDirectory}/temp/{cipher.Id}/{cipher.OrganizationId}/{attachmentData.AttachmentId}";
            var destPath = $"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}";
            var rollBackPath = $"{tempDirectory}/temp/{cipher.Id}/{attachmentData.AttachmentId}";
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
            File.WriteAllText(sourcePath, source);

            await sutProvider.Sut.StartShareAttachmentAsync(cipher.Id, cipher.OrganizationId.Value, attachmentData);

            Assert.True(File.Exists(sourcePath));
            Assert.Equal(source, File.ReadAllText(sourcePath));
            Assert.False(File.Exists(destPath));
            Assert.False(File.Exists(rollBackPath));
        }
    }


    [Theory]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutKey) })]
    public async Task StartShareAttachmentAsync_Success(string source, string destOriginal, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            await StartShareAttachmentAsync(source, destOriginal, cipher, attachmentData, tempDirectory);
        }
    }

    [Theory]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(OrganizationCipher), typeof(MetaDataWithoutKey) })]
    public async Task RollbackShareAttachmentAsync_Success(string source, string destOriginal, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            var sourcePath = $"{tempDirectory}/temp/{cipher.Id}/{cipher.OrganizationId}/{attachmentData.AttachmentId}";
            var destPath = $"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}";
            var rollBackPath = $"{tempDirectory}/temp/{cipher.Id}/{attachmentData.AttachmentId}";

            await StartShareAttachmentAsync(source, destOriginal, cipher, attachmentData, tempDirectory);
            await sutProvider.Sut.RollbackShareAttachmentAsync(cipher.Id, cipher.OrganizationId.Value, attachmentData, "Not Used Here");

            Assert.True(File.Exists(destPath));
            Assert.Equal(destOriginal, File.ReadAllText(destPath));
            Assert.False(File.Exists(sourcePath));
            Assert.False(File.Exists(rollBackPath));
        }
    }

    [Theory]
    [InlineCustomAutoData(new[] { typeof(UserCipher), typeof(MetaData) })]
    [InlineCustomAutoData(new[] { typeof(UserCipher), typeof(MetaDataWithoutContainer) })]
    [InlineCustomAutoData(new[] { typeof(UserCipher), typeof(MetaDataWithoutKey) })]
    public async Task DeleteAttachmentAsync_Success(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            var expectedPath = $"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}";
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));
            File.Create(expectedPath).Close();

            await sutProvider.Sut.DeleteAttachmentAsync(cipher.Id, attachmentData);

            Assert.False(File.Exists(expectedPath));
        }
    }

    [Theory, BitAutoData]
    [UserCipherCustomize]
    public async Task UserCipher_CleanupAsync_Success(Cipher cipher) => await CleanupAsync_Success(cipher);
    [Theory, BitAutoData]
    [OrganizationCipherCustomize]
    public async Task OrganizationCipher_CleanupAsync_Success(Cipher cipher) => await CleanupAsync_Success(cipher);

    private async Task CleanupAsync_Success(Cipher cipher)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            var tempPath = $"{tempDirectory}/temp/{cipher.Id}";
            var permPath = $"{tempDirectory}/{cipher.Id}";
            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(permPath);

            await sutProvider.Sut.CleanupAsync(cipher.Id);

            Assert.False(Directory.Exists(tempPath));
            Assert.True(Directory.Exists(permPath));
        }
    }

    [Theory, BitAutoData]
    [UserCipherCustomize]
    public async Task UserCipher_DeleteAttachmentsForCipherAsync_Success(Cipher cipher) => await DeleteAttachmentsForCipherAsync_Success(cipher);
    [Theory, BitAutoData]
    [OrganizationCipherCustomize]
    public async Task OrganizationCipher_DeleteAttachmentsForCipherAsync_Success(Cipher cipher) => await DeleteAttachmentsForCipherAsync_Success(cipher);

    private async Task DeleteAttachmentsForCipherAsync_Success(Cipher cipher)
    {
        using (var tempDirectory = new TempDirectory())
        {
            var sutProvider = GetSutProvider(tempDirectory);

            var tempPath = $"{tempDirectory}/temp/{cipher.Id}";
            var permPath = $"{tempDirectory}/{cipher.Id}";
            Directory.CreateDirectory(tempPath);
            Directory.CreateDirectory(permPath);

            await sutProvider.Sut.DeleteAttachmentsForCipherAsync(cipher.Id);

            Assert.True(Directory.Exists(tempPath));
            Assert.False(Directory.Exists(permPath));
        }
    }

    private async Task StartShareAttachmentAsync(string source, string destOriginal, Cipher cipher,
        CipherAttachment.MetaData attachmentData, TempDirectory tempDirectory)
    {
        var sutProvider = GetSutProvider(tempDirectory);

        var sourcePath = $"{tempDirectory}/temp/{cipher.Id}/{cipher.OrganizationId}/{attachmentData.AttachmentId}";
        var destPath = $"{tempDirectory}/{cipher.Id}/{attachmentData.AttachmentId}";
        var rollBackPath = $"{tempDirectory}/temp/{cipher.Id}/{attachmentData.AttachmentId}";
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
        File.WriteAllText(sourcePath, source);
        File.WriteAllText(destPath, destOriginal);

        await sutProvider.Sut.StartShareAttachmentAsync(cipher.Id, cipher.OrganizationId.Value, attachmentData);

        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(destPath));
        Assert.Equal(source, File.ReadAllText(destPath));
        Assert.True(File.Exists(rollBackPath));
        Assert.Equal(destOriginal, File.ReadAllText(rollBackPath));
    }

    private SutProvider<LocalAttachmentStorageService> GetSutProvider(TempDirectory tempDirectory)
    {
        var fixture = new Fixture().WithAutoNSubstitutions();
        fixture.Freeze<IGlobalSettings>().Attachment.BaseDirectory.Returns(tempDirectory.Directory);
        fixture.Freeze<IGlobalSettings>().Attachment.BaseUrl.Returns(Guid.NewGuid().ToString());

        return new SutProvider<LocalAttachmentStorageService>(fixture).Create();
    }
}
