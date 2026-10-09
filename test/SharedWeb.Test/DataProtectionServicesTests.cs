using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure;
using Azure.Storage.Blobs;
using Bit.Core.Settings;
using Bit.SharedWeb.Utilities;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Bit.SharedWeb.Test.DataProtectionServicesTests;

public class DataProtectionServicesTests
{
    private const string AzuriteImage =
        "mcr.microsoft.com/azure-storage/azurite:3.37.0@sha256:830430c1da1a2d537e08f3e6764dd1f5ae00cf0346bcaf625b968ec3f0971fd5";

    // Created using:
    // using var rsa = RSA.Create(2048);
    // var now = DateTimeOffset.UtcNow;
    // var certificate = new CertificateRequest("CN=Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
    //    .CreateSelfSigned(now, now.AddDays(365));
    // var data = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, "Alongside-Unworthy-Query3-Cozy"));
    private static readonly byte[] FakeInitialCert = Convert.FromBase64String(@"
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
PZBRQ4YxBFDFaGycVn8CAgfQ");

    /// <summary>
    /// Grabbed from the keys.xml blob during the same test run as the protected data in <see cref="SavedProtectedData" />
    /// </summary>
    private const string KeysData = """<?xml version="1.0" encoding="utf-8"?><repository><key id="6417072d-bd2e-4737-8995-fd280fe00251" version="1"><creationDate>2026-04-29T15:06:08.62851Z</creationDate><activationDate>2026-04-29T15:06:08.549389Z</activationDate><expirationDate>2026-07-28T15:06:08.549389Z</expirationDate><descriptor deserializerType="Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel.AuthenticatedEncryptorDescriptorDeserializer, Microsoft.AspNetCore.DataProtection, Version=8.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60"><descriptor><encryption algorithm="AES_256_CBC" /><validation algorithm="HMACSHA256" /><encryptedSecret decryptorType="Microsoft.AspNetCore.DataProtection.XmlEncryption.EncryptedXmlDecryptor, Microsoft.AspNetCore.DataProtection, Version=8.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60" xmlns="http://schemas.asp.net/2015/03/dataProtection"><EncryptedData Type="http://www.w3.org/2001/04/xmlenc#Element" xmlns="http://www.w3.org/2001/04/xmlenc#"><EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#aes256-cbc" /><KeyInfo xmlns="http://www.w3.org/2000/09/xmldsig#"><EncryptedKey xmlns="http://www.w3.org/2001/04/xmlenc#"><EncryptionMethod Algorithm="http://www.w3.org/2001/04/xmlenc#rsa-1_5" /><KeyInfo xmlns="http://www.w3.org/2000/09/xmldsig#"><X509Data><X509Certificate>MIIC0zCCAbugAwIBAgIJAO8fwy6R8ufxMA0GCSqGSIb3DQEBCwUAMCkxJzAlBgNVBAMTHkRhdGFwcm90ZWN0ZWQgdGVzdCBjZXJ0aWZpY2F0ZTAeFw0yNjA0MjkxNDM1NDVaFw0yNzA0MjkxNDM1NDVaMCkxJzAlBgNVBAMTHkRhdGFwcm90ZWN0ZWQgdGVzdCBjZXJ0aWZpY2F0ZTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAMHISb+ZGeieAG5Vq0oj9IEfYupBZX082uox1RG/J32YjIDIapbjmzNmbNDlefmRSJG8jM4rGpYU/HbB/J0bBNA0miN2okomTs3TOQeHyVbg7d8iomHy8y3El4nSOgkXUKo8Q3tScUNURf6x5DhoKqj1RAHOZ6dp5pYtZmmD4CTpcyODDE2asxgSrX4DuPNoFw0nONWqHydQJKqabUFSHq96FRInwrG0BdFIIxu/XQ61n7Gzd2xh1x2aSI7u3J3tqZOfsw3S2mk1wJ37MENAvYWa8zk/cqOVJ2EPrfXzcde2W9H4JrF56Api6m2yI4WtQWAATRS5KsNwVxuC6QyKfo0CAwEAATANBgkqhkiG9w0BAQsFAAOCAQEAaEcqhxqk2Fki6j9kOmiDRzikuTijUIkd0J2ZaPxg1HSM8mLuVLzy6XMuqSddb9NcfAl+4bKHO8WQqiiAR8pan2ZiQhRzKFS+qTWGbsDoXhYz+etWqrxAf1y+6LEwb4+vSIt7XiXRMBaU2f0690HZy1tUhlTKOrg0t6dM9QubcrWbrG24Kkj6EcQAo+2Mk9kOUO20oI1eN+GgjbxTLJZBb0uQwhJzZx6Td0CVnNpsBHqVr+f4t6LyCGUftPEmSfl6QRgKlIQ1HNZVh2t1oglgBCgv4DuA1dGuZhDBCUfrx5tVPv9wtr5+B87e4bTL/pYBJBuPuBmcJ13GuI6QtaNkIQ==</X509Certificate></X509Data></KeyInfo><CipherData><CipherValue>EAlrlHYiE202m13tlCHvuGpsW0dWnL3EvbTfogF7a3NG28q5s3xgbj7k8bNuuHHTQ9VWlZopXv+DGjWKaPuDGHzs4zYR7YBbdgq3kqoPzayqH7h38IEPYbeJUhbpAZPewWlO5YEa1u7m7ETc6TOFD5iRVYfYZ2P8nMy9DU8fXWDLb3zmYxgRdJYF01YhbBEpPnp572+PthZVfXl8u0Nmf/T72h/QPhaeGIGmdhzb3P6CsISU7+sXRvSRVK5ubsf278elewFVuujJgfC6uGQ7+HfTL9kdDaFXErXNzov9tQqVshYo6JBucOjp4I+q6+uhPBN1QJlssp7+/+ftzDb3Pw==</CipherValue></CipherData></EncryptedKey></KeyInfo><CipherData><CipherValue>MKn3PmIeUPv27Pp8q8+hF/J3LWV3CG06qcP0gBXzpG5G9uVclLBG47+Wv9nZpX3E/qxoKvJFsDxUkqBYYlycj/QuPiM00po4YhkVBdjZrO5B9Zdjmm2uoTQxXvp7HCsV0VlFpqNVnjWl6VxFz0W7qGHfnhCKkW1FWy1ymf2yWvQHkVMlvRcZ1+s35RFnB/Szr89DCL6hWFmBso54QXz80zweNcoCIeN0YdAUEFhnTO0b9L3wu6k++/+eq1OloiMo/+1YQyfq2W1ZeJ3tRaeg8WEdNQnwfMlbRFNmGemExs54wOeXh0WYXqGvI1MTQd7gvO18+1D9AzQmt2tmN7V8j5f1O1OjPEoG0Djl/QE7USBKPLyzEC6fnbFOfaBscy1zp5YTRcjOHKoTm5E6SQLGjg==</CipherValue></CipherData></EncryptedData></encryptedSecret></descriptor></descriptor></key></repository>""";
    /// <summary>
    /// This value was created by grabbing a value from <see cref="SimpleRoundTrip"/> in the debugger.
    /// </summary>
    private const string SavedProtectedData = "CfDJ8C0HF2QuvTdHiZX9KA_gAlGVSrFxAFk2m-WNRwn3MInU65VHLvlnNTKxpGKXDLSY3mgis0FninNa5hpRGIi5KS215GmhPnm-TvuikZr1N3-ib42KqADgmo1i5PTgZohmRA";

    [Fact]
    public async Task SimpleRoundTrip()
    {
        // A very simple show case of how data protection works during a single "run" of our application.
        await RunTestAsync(
            testSetup: async (context) =>
            {
                await context.Certificates.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
                await context.DataProtection.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

                context.Config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
                });
            },
            test: (context) =>
            {
                var protectedData = context.Protector.Protect("MyTestData");
                Assert.Equal("MyTestData", context.Protector.Unprotect(protectedData));
            }
        );
    }

    [Fact]
    public async Task StorageManaged_PersistsUnwrappedKeysWithoutAcquiringCertificates()
    {
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);
        var dataProtection = (await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection")).Value;
        var configuration = new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:KeyProtectionPolicy", "StorageManaged" },
            { "GlobalSettings:DataProtection:CertificatePassword", "Unusable-Protection-Password" },
            { "GlobalSettings:DataProtection:BlobName", "missing-protection-certificate.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "missing-unprotect-certificate.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Unusable-Unprotect-Password" },
        };

        string protectedData;
        using (var firstApp = CreateApp(configuration))
        {
            protectedData = GetProtector(firstApp).Protect("StorageManagedData");
        }

        var keyRingBlob = dataProtection.GetBlobClient("keys.xml");
        Assert.True((await keyRingBlob.ExistsAsync()).Value);
        var keyRing = (await keyRingBlob.DownloadContentAsync()).Value.Content.ToString();
        Assert.DoesNotContain("encryptedSecret", keyRing);

        using var secondApp = CreateApp(configuration);
        Assert.Equal("StorageManagedData", GetProtector(secondApp).Unprotect(protectedData));
    }

    [Fact]
    public async Task UnprotectsSavedData()
    {
        // This shows a somewhat realistic example of how our production cert setup works. We have a cert
        // for encrypting keys and we have a blob that stores all those keys. As long as those keys and that
        // cert are unchanged you should be able to unprotect data even if it was stored and retrieved during
        // a new instance of the application.
        await RunTestAsync(
            testSetup: async (context) =>
            {
                await context.Certificates.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
                await context.DataProtection.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

                context.Config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
                });
            },
            test: (context) =>
            {
                Assert.Equal("MyTestData", context.Protector.Unprotect(SavedProtectedData));
            }
        );
    }

    [Fact]
    public async Task UnprotectsSavedData_ButWithDifferentKeysProtectionCert_Fails()
    {
        // This shows a scenario where you just decide to rip out the existing certificate we use to encrypt keys
        // and give it a new one. It shows that you will be unable to unprotect existing data but you will be able to
        // successfully protect and unprotect new data. This is unacceptable in our case as we have protected data in
        // the database (and other in flight data) that would brick our application.
        await RunTestAsync(
            testSetup: async (context) =>
            {
                // Upload a totally different certificate for encrypting keys with
                using var rsa = RSA.Create(2048);
                var now = DateTimeOffset.UtcNow;
                var certificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                    .CreateSelfSigned(now, now.AddDays(365));

                await context.Certificates.UploadBlobAsync(
                    "dataprotection.pfx",
                    new BinaryData(certificate.Export(X509ContentType.Pfx, "County-Secluded9-Reshuffle-Womanhood"))
                );

                context.Config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "GlobalSettings:DataProtection:CertificatePassword", "County-Secluded9-Reshuffle-Womanhood" },
                });

                // Upload keys that were encrypted with the initial cert
                await context.DataProtection.UploadBlobAsync("keys.xml", new BinaryData(KeysData));
            },
            test: (context) =>
            {
                var newProtectedData = context.Protector.Protect("NewData");
                Assert.Equal("NewData", context.Protector.Unprotect(newProtectedData));

                var cryptographicException = Assert.Throws<CryptographicException>(() => context.Protector.Unprotect(SavedProtectedData));
                Assert.Equal("Unable to retrieve the decryption key.", cryptographicException.Message);
            }
        );
    }

    [Fact]
    public async Task UnprotectSavedData_NewEncryptionCertificate_OldUnprotectCertificateAvailable_Works()
    {
        // This test shows how you are able to work around the issue of wanting to use a new certificate for encrypting
        // new keys but you don't want to brick the application and still allow the old certificate just for unprotection.
        await RunTestAsync(
            testSetup: async (context) =>
            {
                // Upload a totally different certificate for encrypting keys with
                using var rsa = RSA.Create(2048);
                var now = DateTimeOffset.UtcNow;
                var certificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                    .CreateSelfSigned(now, now.AddDays(365));

                await context.Certificates.UploadBlobAsync(
                    "dataprotection.pfx",
                    new BinaryData(certificate.Export(X509ContentType.Pfx, "County-Secluded9-Reshuffle-Womanhood"))
                );

                await context.Certificates.UploadBlobAsync("newcert.pfx", new BinaryData(FakeInitialCert));

                context.Config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "GlobalSettings:DataProtection:CertificatePassword", "County-Secluded9-Reshuffle-Womanhood" },
                    { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "newcert.pfx" },
                    { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
                });

                // Upload keys that were encrypted with the initial cert
                await context.DataProtection.UploadBlobAsync("keys.xml", new BinaryData(KeysData));
            },
            test: (context) =>
            {
                var unprotected = context.Protector.Unprotect(SavedProtectedData);
                Assert.Equal("MyTestData", unprotected);
            }
        );
    }

    [Fact]
    public async Task UpgradePath()
    {
        // The goal of this test is an upgrade scenario where we want to start using a new certificate
        // encrypting keys at rest but want the upgrade to cause 0 issues with data protection
        // we also want to be able to revert the configuration changes in case there are issues.
        //
        // NOTE: This test assumes all config changes land atomically. In practice secrets can land
        // before non-secrets, which makes several steps here unsafe as written. Do not use this
        // test as a basis for what to do in production — see UpgradePath_WithPendingProtection
        // for a deployment sequence that is safe regardless of the order changes land.

        // Setup "existing" azure infrastructure.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        // End existing infrastructure

        // Step 1: We deploy a new version of our app but with NO config changes
        using var noNewConfigApp = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
        });

        var noNewConfigProtector = GetProtector(noNewConfigApp);

        // App should still be able to unprotect data previously protected
        Assert.Equal("MyTestData", noNewConfigProtector.Unprotect(SavedProtectedData));
        // It should also be able to protect new data that will be consumed later
        var noNewConfigProtectedData = AssertRoundTrippable(noNewConfigProtector, "NoNewConfig");

        // Step 2: We generate a new certificate and upload it to a DIFFERENT blob in azure
        // Importantly this can be done at any point.
        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var certificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));

        const string NewCertPassword = "Undergrad-Police0-Maturely-Countless";
        await certificates.Value.UploadBlobAsync(
            "mynewcert.pfx",
            new BinaryData(certificate.Export(X509ContentType.Pfx, NewCertPassword))
        );

        // Step 3: Start apps that have that new cert as able to Unprotect ONLY, this step
        // should have 0 behavioral difference between the previous version but it's important
        // to get all pods with this config so that in the next step all pods don't have to
        // be updated at the same moment. This makes it so that pods that are slower to get the
        // config change are able to interoperate with pods that get it quicker.
        var preparedAppConfig = new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },

            // The new cert gets prepared to be able to unprotect data but it has never been used
            // to protect data so its existence here is technically not needed.
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", NewCertPassword },
        };
        using var preparedApp = CreateApp(preparedAppConfig);

        var preparedAppProtector = GetProtector(preparedApp);

        var preparedProtectedData = AssertRoundTrippable(preparedAppProtector, "Prepared");

        // This app should be able to unprotect data from before any of these changes
        // and from the app that contains only the code deploy and no config changes.
        Assert.Equal("MyTestData", preparedAppProtector.Unprotect(SavedProtectedData));
        Assert.Equal("NoNewConfig", preparedAppProtector.Unprotect(noNewConfigProtectedData));

        // Step 4: This is where real config changes start to happen, we actually start protecting
        // data with the new cert but should still be able to unprotect all previous data
        using var updatedConfigApp = CreateApp(new Dictionary<string, string?>
        {
            // Same connection string as always
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },

            // This config key gets set to the new certificate password
            { "GlobalSettings:DataProtection:CertificatePassword", NewCertPassword },
            // This is a totally new config key and it gets set to the blob where the new
            // cert resides
            { "GlobalSettings:DataProtection:BlobName", "mynewcert.pfx" },

            // The pre-existing certificate gets "demoted" to being a unprotect certificate
            // this is what makes it so that the data can continue to be decrypted
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
        });

        var updatedConfigProtector = GetProtector(updatedConfigApp);

        var updatedConfigData = AssertRoundTrippable(updatedConfigProtector, "UpdatedConfig");

        // This should still be able to unprotect all previously protected data
        Assert.Equal("MyTestData", updatedConfigProtector.Unprotect(SavedProtectedData));
        Assert.Equal("NoNewConfig", updatedConfigProtector.Unprotect(noNewConfigProtectedData));
        Assert.Equal("Prepared", updatedConfigProtector.Unprotect(preparedProtectedData));

        // Problems! If there are problems in step 4 then we should revert the config changes
        // back to what they were in step 3, it will hopefully still be able to unprotect
        // that was actually protected with the new cert but we are still in undefined territory
        // if there are issues as this test intends to show how it will work.
        using var revertedApp = CreateApp(preparedAppConfig);

        var revertedAppProtector = GetProtector(revertedApp);

        AssertRoundTrippable(revertedAppProtector, "Reverted");

        Assert.Equal("MyTestData", revertedAppProtector.Unprotect(SavedProtectedData));
        Assert.Equal("NoNewConfig", revertedAppProtector.Unprotect(noNewConfigProtectedData));
        Assert.Equal("Prepared", revertedAppProtector.Unprotect(preparedProtectedData));
        Assert.Equal("UpdatedConfig", revertedAppProtector.Unprotect(updatedConfigData));
    }

    [Fact]
    public async Task UpgradePath_WithPendingProtection()
    {
        // Full lifecycle of a protect-cert rotation using PendingProtection, including cleanup.
        // Each phase uses the Enabled=false staging pattern so secrets can land before non-secrets.
        //
        // Step A — move old cert into UnprotectCertificates before activating PendingProtection:
        //   Non-secret:   UnprotectCertificates:0:Enabled = false   (inert slot)
        //   Secret lands: UnprotectCertificates:0:Password = OldPw  (safe, slot inactive)
        //   Non-secrets:  UnprotectCertificates:0:FileName + Enabled = true
        //
        // Step B — activate PendingProtection:
        //   Non-secret:   PendingProtection:Enabled = false          (inert slot)
        //   Secret lands: PendingProtection:Password = NewPw         (safe, slot inactive)
        //   Non-secrets:  PendingProtection:FileName + Enabled = true
        //
        // Step C — cleanup (BlobName/CertificatePassword ignored while pending active,
        //           so secret/non-secret order no longer matters for them):
        //   Secret:     CertificatePassword = NewPw
        //   Non-secret: BlobName = "mynewcert.pfx"
        //   Non-secret: PendingProtection:Enabled = false  → falls back to updated BlobName/CertificatePassword
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);
        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var newCertificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));

        const string NewCertPassword = "Undergrad-Police0-Maturely-Countless";
        await certificates.Value.UploadBlobAsync(
            "mynewcert.pfx",
            new BinaryData(newCertificate.Export(X509ContentType.Pfx, NewCertPassword))
        );

        // A1: Old cert password staged, Enabled=false keeps the slot inert — old cert still protects
        using var stepA1 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Enabled", "false" },
        });
        Assert.Equal("MyTestData", GetProtector(stepA1).Unprotect(SavedProtectedData));

        // A2: FileName + Enabled=true land together; old cert now explicitly in UnprotectCertificates
        using var stepA2 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
        });
        Assert.Equal("MyTestData", GetProtector(stepA2).Unprotect(SavedProtectedData));

        // B1: New cert password staged, Enabled=false keeps the slot inert — old cert still protects
        using var stepB1 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:PendingProtection:Password", NewCertPassword },
            { "GlobalSettings:DataProtection:PendingProtection:Enabled", "false" },
        });
        Assert.Equal("MyTestData", GetProtector(stepB1).Unprotect(SavedProtectedData));

        // B2: FileName + Enabled=true land together; new cert now protects; old cert in
        // UnprotectCertificates keeps existing keys readable
        using var stepB2 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:PendingProtection:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:PendingProtection:Password", NewCertPassword },
            { "GlobalSettings:DataProtection:PendingProtection:Enabled", "true" },
        });
        Assert.Equal("MyTestData", GetProtector(stepB2).Unprotect(SavedProtectedData));
        var step4ProtectedData = AssertRoundTrippable(GetProtector(stepB2), "Step4");

        // C1: CertificatePassword updated to new cert's value (secret lands first during cleanup).
        // BlobName still points to old cert blob. Since PendingProtection:Enabled=true,
        // BlobName/CertificatePassword are not loaded — no mismatch, no failure.
        using var stepC1 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", NewCertPassword },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:PendingProtection:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:PendingProtection:Password", NewCertPassword },
            { "GlobalSettings:DataProtection:PendingProtection:Enabled", "true" },
        });
        Assert.Equal("MyTestData", GetProtector(stepC1).Unprotect(SavedProtectedData));
        Assert.Equal("Step4", GetProtector(stepC1).Unprotect(step4ProtectedData));

        // C2: BlobName updated to new cert blob (non-secret lands); still ignored while
        // PendingProtection:Enabled=true, so the old-cert-blob/new-password mismatch window
        // never exists.
        using var stepC2 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:BlobName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:CertificatePassword", NewCertPassword },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:PendingProtection:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:PendingProtection:Password", NewCertPassword },
            { "GlobalSettings:DataProtection:PendingProtection:Enabled", "true" },
        });
        Assert.Equal("MyTestData", GetProtector(stepC2).Unprotect(SavedProtectedData));
        Assert.Equal("Step4", GetProtector(stepC2).Unprotect(step4ProtectedData));

        // C3: PendingProtection deactivated — falls back to BlobName=mynewcert.pfx +
        // CertificatePassword=NewPw via the standard path. PendingProtection block can now
        // be removed entirely from config.
        using var stepC3 = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:BlobName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:CertificatePassword", NewCertPassword },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "dataprotection.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Alongside-Unworthy-Query3-Cozy" },
        });
        Assert.Equal("MyTestData", GetProtector(stepC3).Unprotect(SavedProtectedData));
        Assert.Equal("Step4", GetProtector(stepC3).Unprotect(step4ProtectedData));
        AssertRoundTrippable(GetProtector(stepC3), "CleanedUp");
    }

    [Fact]
    public async Task PendingProtection_WithNoCertificatePassword_Works()
    {
        // PendingProtection:Enabled=true must fire even when CertificatePassword is absent.
        // Previously, PendingProtection was nested inside the CertificatePassword branch, so
        // an absent/placeholder CertificatePassword silently skipped pending and caused a
        // misleading "check your blob storage connection string" startup failure.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);
        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var newCertificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));

        const string NewCertPassword = "Undergrad-Police0-Maturely-Countless";
        await certificates.Value.UploadBlobAsync(
            "mynewcert.pfx",
            new BinaryData(newCertificate.Export(X509ContentType.Pfx, NewCertPassword))
        );
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        // CertificatePassword is intentionally absent — only PendingProtection is configured.
        using var app = CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:PendingProtection:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:PendingProtection:Password", NewCertPassword },
            { "GlobalSettings:DataProtection:PendingProtection:Enabled", "true" },
        });

        var protector = GetProtector(app);
        AssertRoundTrippable(protector, "PendingNoCertPassword");
    }

    [Fact]
    public async Task UnprotectCertificateMissingFromBlobStorage_Throws()
    {
        // The previous implementation returned null on BlobNotFound and that null reached
        // UnprotectKeysWithAnyCertificate, so pods came up, failed to unprotect existing keys
        // with a NullReferenceException, and auto-generated their own keys — producing a
        // divergent key ring across a rolling deploy. The current implementation throws
        // InvalidOperationException at startup with the call-site context ("Unprotect 0") in
        // the message so operators can tell which entry went wrong from the log alone.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        // The unprotect cert blob "mynewcert.pfx" is intentionally never uploaded.
        var exception = Assert.Throws<InvalidOperationException>(() => CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Undergrad-Police0-Maturely-Countless" },
        }));
        Assert.Contains("Unable to download certificate from azure blob storage", exception.Message);
        Assert.Contains("Unprotect 0", exception.Message);
        Assert.IsType<RequestFailedException>(exception.InnerException);
    }

    [Fact]
    public async Task UnprotectCertificatePasswordIncorrect_Throws()
    {
        // The previous implementation swallowed the X509 load failure and returned null, which
        // reached UnprotectKeysWithAnyCertificate with the same downstream effects as a missing
        // blob: pods failed to unprotect existing keys with a NullReferenceException and
        // auto-generated their own divergent keys across a rolling deploy. The current
        // implementation throws InvalidOperationException at startup with the call-site context
        // ("Unprotect 0") in the message and the underlying CryptographicException as the inner
        // exception so operators can tell which entry went wrong from the log alone.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        // The unprotect cert blob is uploaded, but the configured password does not match the
        // password the cert was exported with ("Alongside-Unworthy-Query3-Cozy").
        await certificates.Value.UploadBlobAsync("mynewcert.pfx", new BinaryData(FakeInitialCert));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:FileName", "mynewcert.pfx" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password", "Wrong-Password-For-Cert" },
        }));
        Assert.Contains("Unable to load certificate downloaded from azure blob storage", exception.Message);
        Assert.Contains("Unprotect 0", exception.Message);
        Assert.IsType<CryptographicException>(exception.InnerException);
    }


    [Fact]
    public async Task UnprotectCertificateCorrectPassword_NoFileName_Throws()
    {
        // This test shows what more than likely went wrong when we initially tried to update the config
        // we staged the password which is a secret and it caused a deploy at a different time than the
        // rest of the information on the unprotect certificate was in the config. That caused it to
        // become an object but with a null `FileName` which led to an ArgumentNullException that was
        // swallowed and `null` was returned. In our new fail-fast code this properly leads to an exception
        // at startup.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var certificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));

        const string NewCertPassword = "Undergrad-Police0-Maturely-Countless";
        await certificates.Value.UploadBlobAsync(
            "mynewcert.pfx",
            new BinaryData(certificate.Export(X509ContentType.Pfx, NewCertPassword))
        );

        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Password",  NewCertPassword },
        }));
        Assert.Contains("Unprotect 0", exception.Message);
        Assert.Contains("FileName", exception.Message);
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnprotectCertificateCorrectPassword_NoFileName_EnabledFalse_Works(bool withPassword)
    {
        // This test shows how you can start to progressively stage information about an unprotect
        // but as long as Enabled=false is in their that unprotect certificate will not attempt to be
        // loaded and an otherwise invalid configuration does not cause startup issues and can be used.
        // An unprotect certificate that has been used to actually protect keys should NEVER be disabled
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        var dataProtection = await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        var certificate = new CertificateRequest("CN=New Dataprotected test certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(now, now.AddDays(365));

        const string NewCertPassword = "Undergrad-Police0-Maturely-Countless";
        await certificates.Value.UploadBlobAsync(
            "mynewcert.pfx",
            new BinaryData(certificate.Export(X509ContentType.Pfx, NewCertPassword))
        );

        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));
        await dataProtection.Value.UploadBlobAsync("keys.xml", new BinaryData(KeysData));

        var config = new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
            { "GlobalSettings:DataProtection:UnprotectCertificates:0:Enabled", "false" },
        };

        if (withPassword)
        {
            config.Add("GlobalSettings:Dataprotection:UnprotectCertificates:0:Password", NewCertPassword);
        }

        using var app = CreateApp(config);
        var protector = GetProtector(app);
        AssertRoundTrippable(protector, "NotEnabled");
        Assert.Equal("MyTestData", protector.Unprotect(SavedProtectedData));
    }

    [Fact]
    public async Task ProtectionCertificateMissingFromBlobStorage_Throws()
    {
        // The previous implementation passed a null certificate to ProtectKeysWithCertificate
        // and the call threw with an opaque error. The current implementation throws
        // InvalidOperationException with the call-site context ("protect") in the message and
        // the underlying RequestFailedException as the inner exception so operators can tell
        // which cert went wrong from the log alone.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);
        await blobServiceClient.CreateBlobContainerAsync("certificates");
        await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        // The protection cert blob (default name "dataprotection.pfx") is intentionally never uploaded.
        var exception = Assert.Throws<InvalidOperationException>(() => CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Alongside-Unworthy-Query3-Cozy" },
        }));
        Assert.Contains("Unable to download certificate from azure blob storage", exception.Message);
        Assert.Contains("protect", exception.Message);
        Assert.IsType<RequestFailedException>(exception.InnerException);
    }

    [Fact]
    public void ThumbprintPlaceholder_InDevelopment_DoesNotThrow()
    {
        // Development does not use certificate key wrapping, so certificate acquisition is
        // skipped entirely. Placeholder thumbprints therefore do not prevent local startup,
        // and data protection retains its existing Development persistence behavior.
        using var services = CreateApp(
            new Dictionary<string, string?>
            {
                { "GlobalSettings:Storage:ConnectionString", "UseDevelopmentStorage=true" },
                { "GlobalSettings:DataProtection:CertificateThumbprint", "___________" },
            },
            environmentName: "Development"
        );

        // No exception during startup; a protector can be successfully created.
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Test");
        var roundTripped = protector.Unprotect(protector.Protect("hello"));
        Assert.Equal("hello", roundTripped);
    }

    [Fact]
    public void ThumbprintNotFound_InProduction_ThrowsWithThumbprintHint()
    {
        // When a thumbprint is configured in a non-development environment but the certificate
        // is not present in the current user's certificate store, the error message should
        // name the missing thumbprint so operators can tell immediately what to install rather
        // than being directed to check the blob storage connection string (which is irrelevant
        // to the thumbprint path).
        var exception = Assert.Throws<InvalidOperationException>(() => CreateApp(
            new Dictionary<string, string?>
            {
                { "GlobalSettings:Storage:ConnectionString", "UseDevelopmentStorage=true" },
                { "GlobalSettings:DataProtection:CertificateThumbprint", "___________" },
            },
            environmentName: "Production"
        ));
        Assert.Contains("___________", exception.Message);
        Assert.Contains("certificate store", exception.Message);
    }

    [Fact]
    public async Task ProtectionCertificatePasswordIncorrect_Throws()
    {
        // The previous implementation swallowed the X509 load failure and returned null, which
        // ProtectKeysWithCertificate rejected with an opaque error. The current implementation
        // throws InvalidOperationException with the call-site context ("protect") in the message
        // and the underlying CryptographicException as the inner exception so operators can tell
        // which cert went wrong from the log alone.
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var certificates = await blobServiceClient.CreateBlobContainerAsync("certificates");
        await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection");

        // The protection cert blob is uploaded, but the configured password does not match the
        // password the cert was exported with ("Alongside-Unworthy-Query3-Cozy").
        await certificates.Value.UploadBlobAsync("dataprotection.pfx", new BinaryData(FakeInitialCert));

        var exception = Assert.Throws<InvalidOperationException>(() => CreateApp(new Dictionary<string, string?>
        {
            { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            { "GlobalSettings:DataProtection:CertificatePassword", "Wrong-Password-For-Cert" },
        }));
        Assert.Contains("Unable to load certificate downloaded from azure blob storage", exception.Message);
        Assert.Contains("protect", exception.Message);
        Assert.IsType<CryptographicException>(exception.InnerException);
    }

    private record TestSetupContext(BlobContainerClient Certificates, BlobContainerClient DataProtection, IConfigurationBuilder Config);

    private record TestRunContext(IServiceProvider Services, IDataProtector Protector);

    private IDataProtector GetProtector(IServiceProvider services)
    {
        return services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Test");
    }

    private string AssertRoundTrippable(IDataProtector protector, string testString)
    {
        var protectedData = protector.Protect(testString);
        Assert.Equal(testString, protector.Unprotect(protectedData));
        return protectedData;
    }

    private static IContainer CreateAzuriteContainer() =>
        new ContainerBuilder(AzuriteImage)
            .WithPortBinding(10000, true)
            .Build();

    private static async Task RunTestAsync(Func<TestSetupContext, Task> testSetup, Action<TestRunContext> test)
    {
        // Start azurite
        await using var azurite = CreateAzuriteContainer();

        await azurite.StartAsync();

        var azuriteConnectionString = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://{azurite.Hostname}:{azurite.GetMappedPublicPort(10000)}/devstoreaccount1;";

        var blobServiceClient = new BlobServiceClient(azuriteConnectionString);

        var configurationBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GlobalSettings:Storage:ConnectionString", azuriteConnectionString },
            });

        var context = new TestSetupContext(
            (await blobServiceClient.CreateBlobContainerAsync("certificates")).Value,
            (await blobServiceClient.CreateBlobContainerAsync("aspnet-dataprotection")).Value,
            configurationBuilder
        );

        await testSetup(context);

        using var serviceProvider = CreateApp(context.Config);

        var runContext = new TestRunContext(
            serviceProvider,
            serviceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Test")
        );

        test(runContext);
    }

    private static ServiceProvider CreateApp(Dictionary<string, string?> initialData, string environmentName = "Production")
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(initialData);
        return CreateApp(configurationBuilder, environmentName);
    }

    private static ServiceProvider CreateApp(IConfigurationBuilder configurationBuilder, string environmentName = "Production")
    {
        var services = new ServiceCollection();

        var configuration = configurationBuilder.Build();

        var globalSettings = new GlobalSettings();
        configuration.GetSection("GlobalSettings").Bind(globalSettings);

        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        services.AddCustomDataProtectionServices(
            environment,
            globalSettings
        );

        return services.BuildServiceProvider();
    }
}
