using System.Text;
using AutoFixture;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Enums.Provider;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using IdentityModel;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class CoreHelpersTests
{
    public static IEnumerable<object[]> _epochTestCases = new[]
    {
        new object[] {new DateTime(2020, 12, 30, 11, 49, 12, DateTimeKind.Utc), 1609328952000L},
    };

    [Fact]
    public void GenerateComb_Success()
    {
        // Arrange & Act
        var comb = CoreHelpers.GenerateComb();

        // Assert
        Assert.NotEqual(Guid.Empty, comb);
        // TODO: Add more asserts to make sure important aspects of
        // the comb are working properly
    }

    public static IEnumerable<object[]> GenerateCombCases = new[]
    {
        new object[]
        {
            Guid.Parse("a58db474-43d8-42f1-b4ee-0c17647cd0c0"), // Input Guid
            new DateTime(2022, 3, 12, 12, 12, 0, DateTimeKind.Utc), // Input Time
            Guid.Parse("a58db474-43d8-42f1-b4ee-ae5600c90cc1"), // Expected Comb
        },
        new object[]
        {
            Guid.Parse("f776e6ee-511f-4352-bb28-88513002bdeb"),
            new DateTime(2021, 5, 10, 10, 52, 0, DateTimeKind.Utc),
            Guid.Parse("f776e6ee-511f-4352-bb28-ad2400b313c1"),
        },
        new object[]
        {
            Guid.Parse("51a25fc7-3cad-497d-8e2f-8d77011648a1"),
            new DateTime(1999, 2, 26, 16, 53, 13, DateTimeKind.Utc),
            Guid.Parse("51a25fc7-3cad-497d-8e2f-8d77011649cd"),
        },
        new object[]
        {
            Guid.Parse("bfb8f353-3b32-4a9e-bef6-24fe0b54bfb0"),
            new DateTime(2024, 10, 20, 1, 32, 16, DateTimeKind.Utc),
            Guid.Parse("bfb8f353-3b32-4a9e-bef6-b20f00195780"),
        }
    };

    [Theory]
    [MemberData(nameof(GenerateCombCases))]
    public void GenerateComb_WithInputs_Success(Guid inputGuid, DateTime inputTime, Guid expectedComb)
    {
        var comb = CoreHelpers.GenerateComb(inputGuid, inputTime);

        Assert.Equal(expectedComb, comb);
    }

    /*
    [Fact]
    public void ToGuidIdArrayTVP_Success()
    {
        // Arrange
        var item0 = Guid.NewGuid();
        var item1 = Guid.NewGuid();

        var ids = new[] { item0, item1 };

        // Act
        var dt = ids.ToGuidIdArrayTVP();

        // Assert
        Assert.Single(dt.Columns);
        Assert.Equal("GuidId", dt.Columns[0].ColumnName);
        Assert.Equal(2, dt.Rows.Count);
        Assert.Equal(item0, dt.Rows[0][0]);
        Assert.Equal(item1, dt.Rows[1][0]);
    }
    */

    // TODO: Test the other ToArrayTVP Methods

    [Theory]
    [InlineData("12345&6789", "123456789")]
    [InlineData("abcdef", "ABCDEF")]
    [InlineData("1!@#$%&*()_+", "1")]
    [InlineData("\u00C6123abc\u00C7", "123ABC")]
    [InlineData("123\u00C6ABC", "123ABC")]
    [InlineData("\r\nHello", "E")]
    [InlineData("\tdef", "DEF")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUV1234567890", "ABCDEFABCDEF1234567890")]
    public void CleanCertificateThumbprint_Success(string input, string output)
    {
        // Arrange & Act
        var sanitizedInput = CoreHelpers.CleanCertificateThumbprint(input);

        // Assert
        Assert.Equal(output, sanitizedInput);
    }

    // TODO: Add more tests
    [Theory]
    [MemberData(nameof(_epochTestCases))]
    public void ToEpocMilliseconds_Success(DateTime date, long milliseconds)
    {
        // Act & Assert
        Assert.Equal(milliseconds, CoreHelpers.ToEpocMilliseconds(date));
    }

    [Theory]
    [MemberData(nameof(_epochTestCases))]
    public void FromEpocMilliseconds(DateTime date, long milliseconds)
    {
        // Act & Assert
        Assert.Equal(date, CoreHelpers.FromEpocMilliseconds(milliseconds));
    }

    [Fact]
    public void SecureRandomString_Success()
    {
        // Arrange & Act
        var @string = CoreHelpers.SecureRandomString(8);

        // Assert
        // TODO: Should probably add more Asserts down the line
        Assert.Equal(8, @string.Length);
    }

    [Theory]
    [InlineData(1, "1 Bytes")]
    [InlineData(-5L, "-5 Bytes")]
    [InlineData(1023L, "1023 Bytes")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1025L, "1 KB")]
    [InlineData(-1023L, "-1023 Bytes")]
    [InlineData(-1024L, "-1 KB")]
    [InlineData(-1025L, "-1 KB")]
    [InlineData(1048575L, "1024 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1048577L, "1 MB")]
    [InlineData(-1048575L, "-1024 KB")]
    [InlineData(-1048576L, "-1 MB")]
    [InlineData(-1048577L, "-1 MB")]
    [InlineData(1073741823L, "1024 MB")]
    [InlineData(1073741824L, "1 GB")]
    [InlineData(1073741825L, "1 GB")]
    [InlineData(-1073741823L, "-1024 MB")]
    [InlineData(-1073741824L, "-1 GB")]
    [InlineData(-1073741825L, "-1 GB")]
    [InlineData(long.MaxValue, "8589934592 GB")]
    public void ReadableBytesSize_Success(long size, string readable)
    {
        // Act & Assert
        Assert.Equal(readable, CoreHelpers.ReadableBytesSize(size));
    }

    [Fact]
    public void CloneObject_Success()
    {
        var original = new { Message = "Message" };

        var copy = CoreHelpers.CloneObject(original);

        Assert.Equal(original.Message, copy.Message);
    }

    [Fact]
    public void ExtendQuery_AddNewParameter_Success()
    {
        // Arrange
        var uri = new Uri("https://bitwarden.com/?param1=value1");

        // Act
        var newUri = CoreHelpers.ExtendQuery(uri,
            new Dictionary<string, string> { { "param2", "value2" } });

        // Assert
        Assert.Equal("https://bitwarden.com/?param1=value1&param2=value2", newUri.ToString());
    }

    [Fact]
    public void ExtendQuery_AddTwoNewParameters_Success()
    {
        // Arrange
        var uri = new Uri("https://bitwarden.com/?param1=value1");

        // Act
        var newUri = CoreHelpers.ExtendQuery(uri,
            new Dictionary<string, string>
            {
                { "param2", "value2" },
                { "param3", "value3" }
            });

        // Assert
        Assert.Equal("https://bitwarden.com/?param1=value1&param2=value2&param3=value3", newUri.ToString());
    }

    [Fact]
    public void ExtendQuery_AddExistingParameter_Success()
    {
        // Arrange
        var uri = new Uri("https://bitwarden.com/?param1=value1&param2=value2");

        // Act
        var newUri = CoreHelpers.ExtendQuery(uri,
            new Dictionary<string, string> { { "param1", "test_value" } });

        // Assert
        Assert.Equal("https://bitwarden.com/?param1=test_value&param2=value2", newUri.ToString());
    }

    [Fact]
    public void ExtendQuery_AddNoParameters_Success()
    {
        // Arrange
        const string startingUri = "https://bitwarden.com/?param1=value1";

        var uri = new Uri(startingUri);

        // Act
        var newUri = CoreHelpers.ExtendQuery(uri, new Dictionary<string, string>());

        // Assert
        Assert.Equal(startingUri, newUri.ToString());
    }

    [Theory]
    [InlineData("bücher.com", "xn--bcher-kva.com")]
    [InlineData("bücher.cömé", "xn--bcher-kva.xn--cm-cja4c")]
    [InlineData("hello@bücher.com", "hello@xn--bcher-kva.com")]
    [InlineData("hello@world.cömé", "hello@world.xn--cm-cja4c")]
    [InlineData("hello@bücher.cömé", "hello@xn--bcher-kva.xn--cm-cja4c")]
    [InlineData("ascii.com", "ascii.com")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void PunyEncode_Success(string text, string expected)
    {
        var actual = CoreHelpers.PunyEncode(text);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetEmbeddedResourceContentsAsync_Success()
    {
        var fileContents = CoreHelpers.GetEmbeddedResourceContentsAsync("data.embeddedResource.txt");
        Assert.Equal("Contents of embeddedResource.txt\n", fileContents.Replace("\r\n", "\n"));
    }

    [Theory, BitAutoData, UserCustomize]
    public void BuildIdentityClaims_BaseClaims_Success(User user, bool isPremium)
    {
        var expected = new Dictionary<string, string>
        {
            { "premium", isPremium ? "true" : "false" },
            { JwtClaimTypes.Email, user.Email },
            { JwtClaimTypes.EmailVerified, user.EmailVerified ? "true" : "false" },
            { JwtClaimTypes.Name, user.Name },
            { "sstamp", user.SecurityStamp },
        }.ToList();

        var actual = CoreHelpers.BuildIdentityClaims(user, Array.Empty<CurrentContextOrganization>(),
            Array.Empty<CurrentContextProvider>(), isPremium);

        foreach (var claim in expected)
        {
            Assert.Contains(claim, actual);
        }
        Assert.Equal(expected.Count, actual.Count);
    }

    [Theory, BitAutoData, UserCustomize]
    public void BuildIdentityClaims_NonCustomOrganizationUserType_Success(User user)
    {
        var fixture = new Fixture().WithAutoNSubstitutions();
        foreach (var organizationUserType in Enum.GetValues<OrganizationUserType>().Except(new[] { OrganizationUserType.Custom }))
        {
            var org = fixture.Create<CurrentContextOrganization>();
            org.Type = organizationUserType;

            var expected = new KeyValuePair<string, string>($"org{organizationUserType.ToString().ToLower()}", org.Id.ToString());
            var actual = CoreHelpers.BuildIdentityClaims(user, new[] { org }, Array.Empty<CurrentContextProvider>(), false);

            Assert.Contains(expected, actual);
        }
    }

    [Theory, BitAutoData, UserCustomize]
    public void BuildIdentityClaims_CustomOrganizationUserClaims_Success(User user, CurrentContextOrganization org)
    {
        var fixture = new Fixture().WithAutoNSubstitutions();
        org.Type = OrganizationUserType.Custom;

        var actual = CoreHelpers.BuildIdentityClaims(user, new[] { org }, Array.Empty<CurrentContextProvider>(), false);
        foreach (var (permitted, claimName) in org.Permissions.ClaimsMap)
        {
            var claim = new KeyValuePair<string, string>(claimName, org.Id.ToString());
            if (permitted)
            {

                Assert.Contains(claim, actual);
            }
            else
            {
                Assert.DoesNotContain(claim, actual);
            }
        }
    }

    [Theory, BitAutoData, UserCustomize]
    public void BuildIdentityClaims_ProviderClaims_Success(User user)
    {
        var fixture = new Fixture().WithAutoNSubstitutions();
        var providers = new List<CurrentContextProvider>();
        foreach (var providerUserType in Enum.GetValues<ProviderUserType>())
        {
            var provider = fixture.Create<CurrentContextProvider>();
            provider.Type = providerUserType;
            providers.Add(provider);
        }

        var claims = new List<KeyValuePair<string, string>>();

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

        var actual = CoreHelpers.BuildIdentityClaims(user, Array.Empty<CurrentContextOrganization>(), providers, false);
        foreach (var claim in claims)
        {
            Assert.Contains(claim, actual);
        }
    }

    public static IEnumerable<object[]> TokenIsValidData()
    {
        return new[]
        {
            new object[]
            {
                "first_part 476669d4-9642-4af8-9b29-9366efad4ed3 test@email.com {0}", // unprotectedTokenTemplate
                "first_part", // firstPart
                "test@email.com", // email
                Guid.Parse("476669d4-9642-4af8-9b29-9366efad4ed3"), // id
                DateTime.UtcNow.AddHours(-1), // creationTime
                12, // expirationInHours
                true, // isValid
            }
        };
    }

    [Theory]
    [MemberData(nameof(TokenIsValidData))]
    public void TokenIsValid_Success(string unprotectedTokenTemplate, string firstPart, string userEmail, Guid id, DateTime creationTime, double expirationInHours, bool isValid)
    {
        var protector = new TestDataProtector(string.Format(unprotectedTokenTemplate, CoreHelpers.ToEpocMilliseconds(creationTime)));

        Assert.Equal(isValid, CoreHelpers.TokenIsValid(firstPart, protector, "protected_token", userEmail, id, expirationInHours));
    }

    private class TestDataProtector : IDataProtector
    {
        private readonly string _token;
        public TestDataProtector(string token)
        {
            _token = token;
        }
        public IDataProtector CreateProtector(string purpose) => throw new NotImplementedException();
        public byte[] Protect(byte[] plaintext) => throw new NotImplementedException();
        public byte[] Unprotect(byte[] protectedData)
        {
            return Encoding.UTF8.GetBytes(_token);
        }
    }

    [Theory]
    [InlineData("hi@email.com", "hi@email.com")] // Short email with no room to obfuscate
    [InlineData("name@email.com", "na**@email.com")] // Can obfuscate
    [InlineData("reallylongnamethatnooneshouldhave@email", "re*******************************@email")] // Really long email and no .com, .net, etc
    [InlineData("name@", "name@")] // @ symbol but no domain
    [InlineData("", "")] // Empty string
    [InlineData(null, null)] // null
    public void ObfuscateEmail_Success(string input, string expected)
    {
        Assert.Equal(expected, CoreHelpers.ObfuscateEmail(input));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user@example.com ")]
    [InlineData("user.name@example.com")]
    public void GetEmailDomain_Success(string email)
    {
        Assert.Equal("example.com", CoreHelpers.GetEmailDomain(email));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("userexample.com")]
    [InlineData("user@")]
    [InlineData("@example.com")]
    [InlineData("user@ex@ample.com")]
    public void GetEmailDomain_ReturnsNull(string wrongEmail)
    {
        Assert.Null(CoreHelpers.GetEmailDomain(wrongEmail));
    }
}
