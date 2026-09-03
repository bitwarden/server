using System.Text;
using Bit.Api.Utilities;
using Bit.Core.Billing.Organizations.Models;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Utilities;

public class ApiHelpersTests
{
    [Fact]
    public async Task ReadJsonFileFromBody_Success()
    {
        var context = Substitute.For<HttpContext>();
        context.Request.ContentLength.Returns(200);
        var bytes = Encoding.UTF8.GetBytes(testFile);
        var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "bitwarden_organization_license", "bitwarden_organization_license.json");


        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(context, formFile);
        Assert.Equal(8, license.Version);
    }

    [Fact]
    public void GetDateRange_NeitherBoundSupplied_ReturnsLastThirtyDays()
    {
        var (start, end) = ApiHelpers.GetDateRange(null, null);

        Assert.Equal(DateTime.UtcNow.Date.AddDays(-30), start);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1), end);
    }

    [Fact]
    public void GetDateRange_OnlyStartSupplied_KeepsStartAndRunsToEndOfToday()
    {
        var suppliedStart = DateTime.UtcNow.AddDays(-3);

        var (start, end) = ApiHelpers.GetDateRange(suppliedStart, null);

        Assert.Equal(suppliedStart, start);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(1).AddMilliseconds(-1), end);
    }

    [Fact]
    public void GetDateRange_OnlyEndSupplied_KeepsEndAndStartsThirtyDaysBefore()
    {
        var suppliedEnd = new DateTime(2026, 6, 8, 14, 9, 35, DateTimeKind.Utc);

        var (start, end) = ApiHelpers.GetDateRange(null, suppliedEnd);

        Assert.Equal(suppliedEnd.AddDays(-30), start);
        Assert.Equal(suppliedEnd, end);
    }

    [Fact]
    public void GetDateRange_OnlyEndSuppliedNearMinValue_ClampsStartInsteadOfThrowing()
    {
        var suppliedEnd = new DateTime(1, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        var (start, end) = ApiHelpers.GetDateRange(null, suppliedEnd);

        Assert.Equal(DateTime.MinValue, start);
        Assert.Equal(suppliedEnd, end);
    }

    [Fact]
    public void GetDateRange_OnlyEndSuppliedExactlyThirtyDaysAfterMinValue_ClampsToMinValue()
    {
        var suppliedEnd = DateTime.MinValue.AddDays(30);

        var (start, _) = ApiHelpers.GetDateRange(null, suppliedEnd);

        Assert.Equal(DateTime.MinValue, start);
    }

    [Fact]
    public void GetDateRange_OnlyStartSuppliedAtMaxValue_ThrowsBadRequestRatherThanOverflowing()
    {
        Assert.Throws<BadRequestException>(() => ApiHelpers.GetDateRange(DateTime.MaxValue, null));
    }

    [Fact]
    public void GetDateRange_BothBoundsSupplied_ReturnsThemUnchanged()
    {
        var suppliedStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var suppliedEnd = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        var (start, end) = ApiHelpers.GetDateRange(suppliedStart, suppliedEnd);

        Assert.Equal(suppliedStart, start);
        Assert.Equal(suppliedEnd, end);
    }

    [Fact]
    public void GetDateRange_InvertedBounds_SwapsThem()
    {
        var earlier = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        var (start, end) = ApiHelpers.GetDateRange(later, earlier);

        Assert.Equal(earlier, start);
        Assert.Equal(later, end);
    }

    [Fact]
    public void GetDateRange_RangeExceedsCap_ThrowsBadRequest()
    {
        var suppliedStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var suppliedEnd = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var exception = Assert.Throws<BadRequestException>(
            () => ApiHelpers.GetDateRange(suppliedStart, suppliedEnd));

        Assert.Equal("Date range must be < 367 days.", exception.Message);
    }

    [Fact]
    public void GetDateRange_OnlyStartSuppliedBeyondCap_ThrowsBadRequest()
    {
        var suppliedStart = DateTime.UtcNow.AddDays(-400);

        Assert.Throws<BadRequestException>(() => ApiHelpers.GetDateRange(suppliedStart, null));
    }

    [Fact]
    public void GetDateRange_OnlyStartSuppliedInTheFuture_SwapsRatherThanInverting()
    {
        var suppliedStart = DateTime.UtcNow.AddDays(3);

        var (start, end) = ApiHelpers.GetDateRange(suppliedStart, null);

        Assert.True(start <= end);
        Assert.Equal(suppliedStart, end);
    }

    const string testFile = "{\"licenseKey\": \"licenseKey\", \"installationId\": \"6285f891-b2ec-4047-84c5-2eb7f7747e74\", \"id\": \"1065216d-5854-4326-838d-635487f30b43\",\"name\": \"Test Org\",\"billingEmail\": \"test@email.com\",\"businessName\": null,\"enabled\": true, \"plan\": \"Enterprise (Annually)\",\"planType\": 11,\"seats\": 6,\"maxCollections\": null,\"usePolicies\": true,\"useSso\": true,\"useKeyConnector\": false,\"useGroups\": true,\"useEvents\": true,\"useDirectory\": true,\"useTotp\": true,\"use2fa\": true,\"useApi\": true,\"useResetPassword\": true,\"maxStorageGb\": 1,\"selfHost\": true,\"usersGetPremium\": true,\"version\": 8,\"issued\": \"2022-01-25T21:58:38.9454581Z\",\"refresh\": \"2022-01-28T14:26:31Z\",\"expires\": \"2022-01-28T14:26:31Z\",\"trial\": true,\"hash\": \"testvalue\",\"signature\": \"signature\"}";
}
