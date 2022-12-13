using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues.Models;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Settings;
using IdentityModel;
using Microsoft.AspNetCore.DataProtection;
using MimeKit;

namespace Bit.Core.Utilities;

public static class CoreHelpers
{
    private static readonly long _baseDateTicks = new DateTime(1900, 1, 1).Ticks;
    private static readonly DateTime _epoc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _max = new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Random _random = new Random();
    private static readonly string CloudFlareConnectingIp = "CF-Connecting-IP";
    private static readonly string RealIp = "X-Real-IP";

    /// <summary>
    /// Generate sequential Guid for Sql Server.
    /// ref: https://github.com/nhibernate/nhibernate-core/blob/master/src/NHibernate/Id/GuidCombGenerator.cs
    /// </summary>
    /// <returns>A comb Guid.</returns>
    public static Guid GenerateComb()
        => GenerateComb(Guid.NewGuid(), DateTime.UtcNow);

    /// <summary>
    /// Implementation of <see cref="GenerateComb()" /> with input parameters to remove randomness.
    /// This should NOT be used outside of testing.
    /// </summary>
    /// <remarks>
    /// You probably don't want to use this method and instead want to use <see cref="GenerateComb()" /> with no parameters
    /// </remarks>
    internal static Guid GenerateComb(Guid startingGuid, DateTime time)
    {
        var guidArray = startingGuid.ToByteArray();

        // Get the days and milliseconds which will be used to build the byte string 
        var days = new TimeSpan(time.Ticks - _baseDateTicks);
        var msecs = time.TimeOfDay;

        // Convert to a byte array 
        // Note that SQL Server is accurate to 1/300th of a millisecond so we divide by 3.333333 
        var daysArray = BitConverter.GetBytes(days.Days);
        var msecsArray = BitConverter.GetBytes((long)(msecs.TotalMilliseconds / 3.333333));

        // Reverse the bytes to match SQL Servers ordering 
        Array.Reverse(daysArray);
        Array.Reverse(msecsArray);

        // Copy the bytes into the guid 
        Array.Copy(daysArray, daysArray.Length - 2, guidArray, guidArray.Length - 6, 2);
        Array.Copy(msecsArray, msecsArray.Length - 4, guidArray, guidArray.Length - 4, 4);

        return new Guid(guidArray);
    }

    public static string CleanCertificateThumbprint(string thumbprint)
    {
        // Clean possible garbage characters from thumbprint copy/paste
        // ref http://stackoverflow.com/questions/8448147/problems-with-x509store-certificates-find-findbythumbprint
        return Regex.Replace(thumbprint, @"[^\da-fA-F]", string.Empty).ToUpper();
    }

    public static X509Certificate2 GetCertificate(string thumbprint)
    {
        thumbprint = CleanCertificateThumbprint(thumbprint);

        X509Certificate2 cert = null;
        var certStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        certStore.Open(OpenFlags.ReadOnly);
        var certCollection = certStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
        if (certCollection.Count > 0)
        {
            cert = certCollection[0];
        }

        certStore.Close();
        return cert;
    }

    public static X509Certificate2 GetCertificate(string file, string password)
    {
        return new X509Certificate2(file, password);
    }

    public async static Task<X509Certificate2> GetEmbeddedCertificateAsync(string file, string password)
    {
        var assembly = typeof(CoreHelpers).GetTypeInfo().Assembly;
        using (var s = assembly.GetManifestResourceStream($"Bit.Core.{file}"))
        using (var ms = new MemoryStream())
        {
            await s.CopyToAsync(ms);
            return new X509Certificate2(ms.ToArray(), password);
        }
    }

    public static string GetEmbeddedResourceContentsAsync(string file)
    {
        var assembly = Assembly.GetCallingAssembly();
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith(file));
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }

    public async static Task<X509Certificate2> GetBlobCertificateAsync(string connectionString, string container, string file, string password)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerRef2 = blobServiceClient.GetBlobContainerClient(container);
            var blobRef = containerRef2.GetBlobClient(file);

            using var memStream = new MemoryStream();
            await blobRef.DownloadToAsync(memStream).ConfigureAwait(false);
            return new X509Certificate2(memStream.ToArray(), password);
        }
        catch (RequestFailedException ex)
        when (ex.ErrorCode == BlobErrorCode.ContainerNotFound || ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static long ToEpocMilliseconds(DateTime date)
    {
        return (long)Math.Round((date - _epoc).TotalMilliseconds, 0);
    }

    public static DateTime FromEpocMilliseconds(long milliseconds)
    {
        return _epoc.AddMilliseconds(milliseconds);
    }

    public static long ToEpocSeconds(DateTime date)
    {
        return (long)Math.Round((date - _epoc).TotalSeconds, 0);
    }

    public static DateTime FromEpocSeconds(long seconds)
    {
        return _epoc.AddSeconds(seconds);
    }

    public static string U2fAppIdUrl(GlobalSettings globalSettings)
    {
        return string.Concat(globalSettings.BaseServiceUri.Vault, "/app-id.json");
    }

    public static string RandomString(int length, bool alpha = true, bool upper = true, bool lower = true,
        bool numeric = true, bool special = false)
    {
        return RandomString(length, RandomStringCharacters(alpha, upper, lower, numeric, special));
    }

    public static string RandomString(int length, string characters)
    {
        return new string(Enumerable.Repeat(characters, length).Select(s => s[_random.Next(s.Length)]).ToArray());
    }

    public static string SecureRandomString(int length, bool alpha = true, bool upper = true, bool lower = true,
        bool numeric = true, bool special = false)
    {
        return SecureRandomString(length, RandomStringCharacters(alpha, upper, lower, numeric, special));
    }

    // ref https://stackoverflow.com/a/8996788/1090359 with modifications
    public static string SecureRandomString(int length, string characters)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "length cannot be less than zero.");
        }

        if ((characters?.Length ?? 0) == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characters), "characters invalid.");
        }

        const int byteSize = 0x100;
        if (byteSize < characters.Length)
        {
            throw new ArgumentException(
                string.Format("{0} may contain no more than {1} characters.", nameof(characters), byteSize),
                nameof(characters));
        }

        var outOfRangeStart = byteSize - (byteSize % characters.Length);
        using (var rng = RandomNumberGenerator.Create())
        {
            var sb = new StringBuilder();
            var buffer = new byte[128];
            while (sb.Length < length)
            {
                rng.GetBytes(buffer);
                for (var i = 0; i < buffer.Length && sb.Length < length; ++i)
                {
                    // Divide the byte into charSet-sized groups. If the random value falls into the last group and the
                    // last group is too small to choose from the entire allowedCharSet, ignore the value in order to
                    // avoid biasing the result.
                    if (outOfRangeStart <= buffer[i])
                    {
                        continue;
                    }

                    sb.Append(characters[buffer[i] % characters.Length]);
                }
            }

            return sb.ToString();
        }
    }

    private static string RandomStringCharacters(bool alpha, bool upper, bool lower, bool numeric, bool special)
    {
        var characters = string.Empty;
        if (alpha)
        {
            if (upper)
            {
                characters += "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            }

            if (lower)
            {
                characters += "abcdefghijklmnopqrstuvwxyz";
            }
        }

        if (numeric)
        {
            characters += "0123456789";
        }

        if (special)
        {
            characters += "!@#$%^*&";
        }

        return characters;
    }

    // ref: https://stackoverflow.com/a/11124118/1090359
    // Returns the human-readable file size for an arbitrary 64-bit file size .
    // The format is "0.## XB", ex: "4.2 KB" or "1.43 GB"
    public static string ReadableBytesSize(long size)
    {
        // Get absolute value
        var absoluteSize = (size < 0 ? -size : size);

        // Determine the suffix and readable value
        string suffix;
        double readable;
        if (absoluteSize >= 0x40000000) // 1 Gigabyte
        {
            suffix = "GB";
            readable = (size >> 20);
        }
        else if (absoluteSize >= 0x100000) // 1 Megabyte
        {
            suffix = "MB";
            readable = (size >> 10);
        }
        else if (absoluteSize >= 0x400) // 1 Kilobyte
        {
            suffix = "KB";
            readable = size;
        }
        else
        {
            return size.ToString("0 Bytes"); // Byte
        }

        // Divide by 1024 to get fractional value
        readable = (readable / 1024);

        // Return formatted number with suffix
        return readable.ToString("0.## ") + suffix;
    }

    /// <summary>
    /// Creates a clone of the given object through serializing to json and deserializing.
    /// This method is subject to the limitations of System.Text.Json. For example, properties with
    /// inaccessible setters will not be set.
    /// </summary>
    public static T CloneObject<T>(T obj)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj));
    }

    public static bool SettingHasValue(string setting)
    {
        var normalizedSetting = setting?.ToLowerInvariant();
        return !string.IsNullOrWhiteSpace(normalizedSetting) && !normalizedSetting.Equals("secret") &&
            !normalizedSetting.Equals("replace");
    }

    public static string Base64EncodeString(string input)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
    }

    public static string Base64DecodeString(string input)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(input));
    }

    public static string Base64UrlEncodeString(string input)
    {
        return Base64UrlEncode(Encoding.UTF8.GetBytes(input));
    }

    public static string Base64UrlDecodeString(string input)
    {
        return Encoding.UTF8.GetString(Base64UrlDecode(input));
    }

    public static string Base64UrlEncode(byte[] input)
    {
        var output = Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", string.Empty);
        return output;
    }

    public static byte[] Base64UrlDecode(string input)
    {
        var output = input;
        // 62nd char of encoding
        output = output.Replace('-', '+');
        // 63rd char of encoding
        output = output.Replace('_', '/');
        // Pad with trailing '='s
        switch (output.Length % 4)
        {
            case 0:
                // No pad chars in this case
                break;
            case 2:
                // Two pad chars
                output += "=="; break;
            case 3:
                // One pad char
                output += "="; break;
            default:
                throw new InvalidOperationException("Illegal base64url string!");
        }

        // Standard base64 decoder
        return Convert.FromBase64String(output);
    }

    public static string PunyEncode(string text)
    {
        if (text == "")
        {
            return "";
        }

        if (text == null)
        {
            return null;
        }

        if (!text.Contains("@"))
        {
            // Assume domain name or non-email address
            var idn = new IdnMapping();
            return idn.GetAscii(text);
        }
        else
        {
            // Assume email address
            return MailboxAddress.EncodeAddrspec(text);
        }
    }

    public static string FormatLicenseSignatureValue(object val)
    {
        if (val == null)
        {
            return string.Empty;
        }

        if (val.GetType() == typeof(DateTime))
        {
            return ToEpocSeconds((DateTime)val).ToString();
        }

        if (val.GetType() == typeof(bool))
        {
            return val.ToString().ToLowerInvariant();
        }

        if (val is PlanType planType)
        {
            return planType switch
            {
                PlanType.Free => "Free",
                PlanType.FamiliesAnnually2019 => "FamiliesAnnually",
                PlanType.TeamsMonthly2019 => "TeamsMonthly",
                PlanType.TeamsAnnually2019 => "TeamsAnnually",
                PlanType.EnterpriseMonthly2019 => "EnterpriseMonthly",
                PlanType.EnterpriseAnnually2019 => "EnterpriseAnnually",
                PlanType.Custom => "Custom",
                _ => ((byte)planType).ToString(),
            };
        }

        return val.ToString();
    }

    public static string SanitizeForEmail(string value, bool htmlEncode = true)
    {
        var cleanedValue = value.Replace("@", "[at]");
        var regexOptions = RegexOptions.CultureInvariant |
            RegexOptions.Singleline |
            RegexOptions.IgnoreCase;
        cleanedValue = Regex.Replace(cleanedValue, @"(\.\w)",
                m => string.Concat("[dot]", m.ToString().Last()), regexOptions);
        while (Regex.IsMatch(cleanedValue, @"((^|\b)(\w*)://)", regexOptions))
        {
            cleanedValue = Regex.Replace(cleanedValue, @"((^|\b)(\w*)://)",
                string.Empty, regexOptions);
        }
        return htmlEncode ? HttpUtility.HtmlEncode(cleanedValue) : cleanedValue;
    }

    public static string DateTimeToTableStorageKey(DateTime? date = null)
    {
        if (date.HasValue)
        {
            date = date.Value.ToUniversalTime();
        }
        else
        {
            date = DateTime.UtcNow;
        }

        return _max.Subtract(date.Value).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
    }

    // ref: https://stackoverflow.com/a/27545010/1090359
    public static Uri ExtendQuery(Uri uri, IDictionary<string, string> values)
    {
        var baseUri = uri.ToString();
        var queryString = string.Empty;
        if (baseUri.Contains("?"))
        {
            var urlSplit = baseUri.Split('?');
            baseUri = urlSplit[0];
            queryString = urlSplit.Length > 1 ? urlSplit[1] : string.Empty;
        }

        var queryCollection = HttpUtility.ParseQueryString(queryString);
        foreach (var kvp in values ?? new Dictionary<string, string>())
        {
            queryCollection[kvp.Key] = kvp.Value;
        }

        var uriKind = uri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative;
        if (queryCollection.Count == 0)
        {
            return new Uri(baseUri, uriKind);
        }
        return new Uri(string.Format("{0}?{1}", baseUri, queryCollection), uriKind);
    }

    public static string CustomProviderName(TwoFactorProviderType type)
    {
        return string.Concat("Custom_", type.ToString());
    }

    public static bool UserInviteTokenIsValid(IDataProtector protector, string token, string userEmail,
        Guid orgUserId, IGlobalSettings globalSettings)
    {
        return TokenIsValid("OrganizationUserInvite", protector, token, userEmail, orgUserId,
            globalSettings.OrganizationInviteExpirationHours);
    }

    public static bool TokenIsValid(string firstTokenPart, IDataProtector protector, string token, string userEmail,
        Guid id, double expirationInHours)
    {
        var invalid = true;
        try
        {
            var unprotectedData = protector.Unprotect(token);
            var dataParts = unprotectedData.Split(' ');
            if (dataParts.Length == 4 && dataParts[0] == firstTokenPart &&
                new Guid(dataParts[1]) == id &&
                dataParts[2].Equals(userEmail, StringComparison.InvariantCultureIgnoreCase))
            {
                var creationTime = FromEpocMilliseconds(Convert.ToInt64(dataParts[3]));
                var expTime = creationTime.AddHours(expirationInHours);
                invalid = expTime < DateTime.UtcNow;
            }
        }
        catch
        {
            invalid = true;
        }

        return !invalid;
    }

    public static string GetApplicationCacheServiceBusSubcriptionName(GlobalSettings globalSettings)
    {
        var subName = globalSettings.ServiceBus.ApplicationCacheSubscriptionName;
        if (string.IsNullOrWhiteSpace(subName))
        {
            var websiteInstanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            if (string.IsNullOrWhiteSpace(websiteInstanceId))
            {
                throw new Exception("No service bus subscription name available.");
            }
            else
            {
                subName = $"{globalSettings.ProjectName.ToLower()}_{websiteInstanceId}";
                if (subName.Length > 50)
                {
                    subName = subName.Substring(0, 50);
                }
            }
        }
        return subName;
    }

    public static string GetIpAddress(this Microsoft.AspNetCore.Http.HttpContext httpContext,
        GlobalSettings globalSettings)
    {
        if (httpContext == null)
        {
            return null;
        }

        if (!globalSettings.SelfHosted && httpContext.Request.Headers.ContainsKey(CloudFlareConnectingIp))
        {
            return httpContext.Request.Headers[CloudFlareConnectingIp].ToString();
        }
        if (globalSettings.SelfHosted && httpContext.Request.Headers.ContainsKey(RealIp))
        {
            return httpContext.Request.Headers[RealIp].ToString();
        }

        return httpContext.Connection?.RemoteIpAddress?.ToString();
    }

    public static bool IsCorsOriginAllowed(string origin, GlobalSettings globalSettings)
    {
        return
            // Web vault
            origin == globalSettings.BaseServiceUri.Vault ||
            // Safari extension origin
            origin == "file://" ||
            // Product website
            (!globalSettings.SelfHosted && origin == "https://bitwarden.com");
    }

    public static X509Certificate2 GetIdentityServerCertificate(GlobalSettings globalSettings)
    {
        if (globalSettings.SelfHosted &&
            SettingHasValue(globalSettings.IdentityServer.CertificatePassword)
            && File.Exists("identity.pfx"))
        {
            return GetCertificate("identity.pfx",
                globalSettings.IdentityServer.CertificatePassword);
        }
        else if (SettingHasValue(globalSettings.IdentityServer.CertificateThumbprint))
        {
            return GetCertificate(
                globalSettings.IdentityServer.CertificateThumbprint);
        }
        else if (!globalSettings.SelfHosted &&
            SettingHasValue(globalSettings.Storage?.ConnectionString) &&
            SettingHasValue(globalSettings.IdentityServer.CertificatePassword))
        {
            return GetBlobCertificateAsync(globalSettings.Storage.ConnectionString, "certificates",
                "identity.pfx", globalSettings.IdentityServer.CertificatePassword).GetAwaiter().GetResult();
        }
        return null;
    }

    public static Dictionary<string, object> AdjustIdentityServerConfig(Dictionary<string, object> configDict,
        string publicServiceUri, string internalServiceUri)
    {
        var dictReplace = new Dictionary<string, object>();
        foreach (var item in configDict)
        {
            if (item.Key == "authorization_endpoint" && item.Value is string val)
            {
                var uri = new Uri(val);
                dictReplace.Add(item.Key, string.Concat(publicServiceUri, uri.LocalPath));
            }
            else if ((item.Key == "jwks_uri" || item.Key.EndsWith("_endpoint")) && item.Value is string val2)
            {
                var uri = new Uri(val2);
                dictReplace.Add(item.Key, string.Concat(internalServiceUri, uri.LocalPath));
            }
        }
        foreach (var replace in dictReplace)
        {
            configDict[replace.Key] = replace.Value;
        }
        return configDict;
    }

    public static List<KeyValuePair<string, string>> BuildIdentityClaims(User user, ICollection<CurrentContentOrganization> orgs,
        ICollection<CurrentContentProvider> providers, bool isPremium)
    {
        var claims = new List<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("premium", isPremium ? "true" : "false"),
            new KeyValuePair<string, string>(JwtClaimTypes.Email, user.Email),
            new KeyValuePair<string, string>(JwtClaimTypes.EmailVerified, user.EmailVerified ? "true" : "false"),
            new KeyValuePair<string, string>("sstamp", user.SecurityStamp)
        };

        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            claims.Add(new KeyValuePair<string, string>(JwtClaimTypes.Name, user.Name));
        }

        // Orgs that this user belongs to
        if (orgs.Any())
        {
            foreach (var group in orgs.GroupBy(o => o.Type))
            {
                switch (group.Key)
                {
                    case Enums.OrganizationUserType.Owner:
                        foreach (var org in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("orgowner", org.Id.ToString()));
                        }
                        break;
                    case Enums.OrganizationUserType.Admin:
                        foreach (var org in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("orgadmin", org.Id.ToString()));
                        }
                        break;
                    case Enums.OrganizationUserType.Manager:
                        foreach (var org in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("orgmanager", org.Id.ToString()));
                        }
                        break;
                    case Enums.OrganizationUserType.User:
                        foreach (var org in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("orguser", org.Id.ToString()));
                        }
                        break;
                    case Enums.OrganizationUserType.Custom:
                        foreach (var org in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("orgcustom", org.Id.ToString()));
                            foreach (var (permission, claimName) in org.Permissions.ClaimsMap)
                            {
                                if (!permission)
                                {
                                    continue;
                                }

                                claims.Add(new KeyValuePair<string, string>(claimName, org.Id.ToString()));
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        if (providers.Any())
        {
            foreach (var group in providers.GroupBy(o => o.Type))
            {
                switch (group.Key)
                {
                    case ProviderUserType.ProviderAdmin:
                        foreach (var provider in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("providerprovideradmin", provider.Id.ToString()));
                        }
                        break;
                    case ProviderUserType.ServiceUser:
                        foreach (var provider in group)
                        {
                            claims.Add(new KeyValuePair<string, string>("providerserviceuser", provider.Id.ToString()));
                        }
                        break;
                }
            }
        }

        return claims;
    }

    public static T LoadClassFromJsonData<T>(string jsonData) where T : new()
    {
        if (string.IsNullOrWhiteSpace(jsonData))
        {
            return new T();
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        return System.Text.Json.JsonSerializer.Deserialize<T>(jsonData, options);
    }

    public static string ClassToJsonData<T>(T data)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        return System.Text.Json.JsonSerializer.Serialize(data, options);
    }

    public static ICollection<T> AddIfNotExists<T>(this ICollection<T> list, T item)
    {
        if (list.Contains(item))
        {
            return list;
        }
        list.Add(item);
        return list;
    }

    public static string DecodeMessageText(this QueueMessage message)
    {
        var text = message?.MessageText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
        try
        {
            return Base64DecodeString(text);
        }
        catch
        {
            return text;
        }
    }

    public static bool FixedTimeEquals(string input1, string input2)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(input1), Encoding.UTF8.GetBytes(input2));
    }

    public static string ObfuscateEmail(string email)
    {
        if (email == null)
        {
            return email;
        }

        var emailParts = email.Split('@', StringSplitOptions.RemoveEmptyEntries);

        if (emailParts.Length != 2)
        {
            return email;
        }

        var username = emailParts[0];

        if (username.Length < 2)
        {
            return email;
        }

        var sb = new StringBuilder();
        sb.Append(emailParts[0][..2]);
        for (var i = 2; i < emailParts[0].Length; i++)
        {
            sb.Append('*');
        }

        return sb.Append('@')
            .Append(emailParts[1])
            .ToString();

    }
}
