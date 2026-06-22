using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Storage.Blobs;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bit.SharedWeb.Utilities;

public static class DataProtectionServiceCollectionExtensions
{
    public static void AddCustomDataProtectionServices(
        this IServiceCollection services, IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        var builder = services.AddDataProtection().SetApplicationName("Bitwarden");

        if (globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.DataProtection.Directory))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(globalSettings.DataProtection.Directory));
        }

        if (!globalSettings.SelfHosted && CoreHelpers.SettingHasValue(globalSettings.Storage?.ConnectionString))
        {
            X509Certificate2? dataProtectionCert = null;
            if (CoreHelpers.SettingHasValue(globalSettings.DataProtection.CertificateThumbprint))
            {
                dataProtectionCert = CoreHelpers.GetCertificate(
                    globalSettings.DataProtection.CertificateThumbprint)
                    ?? throw new InvalidOperationException(
                        $"No data protection certificate could be found with thumbprint '{globalSettings.DataProtection.CertificateThumbprint}'.");
            }
            else if (CoreHelpers.SettingHasValue(globalSettings.DataProtection.CertificatePassword))
            {
                dataProtectionCert = DownloadRequiredCertFromBlobStorage(
                    globalSettings.Storage.ConnectionString,
                    "certificates",
                    globalSettings.DataProtection.BlobName,
                    globalSettings.DataProtection.CertificatePassword,
                    "protect"
                );
            }

            if (!env.IsDevelopment())
            {
                if (dataProtectionCert is null)
                {
                    throw new InvalidOperationException("A data protection certificate could not be acquired and one is required when running in non-development cloud environments. Please make sure your configuration has a valid connection string to azure blob storage.");
                }

                builder
                    .PersistKeysToAzureBlobStorage(globalSettings.Storage.ConnectionString, "aspnet-dataprotection", "keys.xml")
                    .ProtectKeysWithCertificate(dataProtectionCert);

                if (globalSettings.DataProtection.UnprotectCertificates.Length > 0)
                {
                    var unprotectCertificates = globalSettings.DataProtection.UnprotectCertificates
                        .Index()
                        .Select(i => DownloadRequiredCertFromBlobStorage(
                            globalSettings.Storage.ConnectionString,
                            "certificates",
                            i.Item.FileName,
                            i.Item.Password,
                            $"Unprotect {i.Index}"
                        )).ToArray();

                    builder.UnprotectKeysWithAnyCertificate(unprotectCertificates);
                }
            }
        }
    }

    private static X509Certificate2 DownloadRequiredCertFromBlobStorage(
        string connectionString,
        string container,
        string file,
        string password,
        string context)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(container);
            var blobClient = containerClient.GetBlobClient(file);

            using var memoryStream = new MemoryStream();
            blobClient.DownloadTo(memoryStream);
            return X509CertificateLoader.LoadPkcs12(memoryStream.ToArray(), password);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException($"Unable to download certificate from azure blob storage: {context}", ex);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException($"Unable to load certificate downloaded from azure blob storage; verify the password is correct and the blob contains valid PKCS#12 data: {context}", ex);
        }
    }
}
