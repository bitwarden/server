using Bit.Api.Models.Request.Accounts;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Bit.Api.Test.Models.Request.Accounts;

public class PremiumRequestModelTests
{
    public static IEnumerable<object[]> GetValidateData()
    {
        // 1. selfHosted
        // 2. formFile
        // 3. country
        // 4. expected

        yield return new object[] { true, null, null, false };
        yield return new object[] { true, null, "US", false };
        yield return new object[] { true, new NotImplementedFormFile(), null, false };
        yield return new object[] { true, new NotImplementedFormFile(), "US", false };

        yield return new object[] { false, null, null, false };
        yield return new object[] { false, null, "US", true }; // Only true, cloud with null license AND a Country
        yield return new object[] { false, new NotImplementedFormFile(), null, false };
        yield return new object[] { false, new NotImplementedFormFile(), "US", false };
    }

    [Theory]
    [MemberData(nameof(GetValidateData))]
    public void Validate_Success(bool selfHosted, IFormFile formFile, string country, bool expected)
    {
        var gs = new GlobalSettings { SelfHosted = selfHosted };

        var sut = new PremiumRequestModel { License = formFile, Country = country };

        Assert.Equal(expected, sut.Validate(gs));
    }
}

public class NotImplementedFormFile : IFormFile
{
    public string ContentType => throw new NotImplementedException();

    public string ContentDisposition => throw new NotImplementedException();

    public IHeaderDictionary Headers => throw new NotImplementedException();

    public long Length => throw new NotImplementedException();

    public string Name => throw new NotImplementedException();

    public string FileName => throw new NotImplementedException();

    public void CopyTo(Stream target) => throw new NotImplementedException();

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Stream OpenReadStream() => throw new NotImplementedException();
}
