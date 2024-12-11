using System.Text;
using Bit.Api.Utilities;
using Bit.Core.Models.Business;
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
        var formFile = new FormFile(
            new MemoryStream(bytes),
            0,
            bytes.Length,
            "bitwarden_organization_license",
            "bitwarden_organization_license.json"
        );

        var license = await ApiHelpers.ReadJsonFileFromBody<OrganizationLicense>(context, formFile);
        Assert.Equal(8, license.Version);
    }

    const string testFile =
        "{\"licenseKey\": \"licenseKey\", \"installationId\": \"6285f891-b2ec-4047-84c5-2eb7f7747e74\", \"id\": \"1065216d-5854-4326-838d-635487f30b43\",\"name\": \"Test Org\",\"billingEmail\": \"test@email.com\",\"businessName\": null,\"enabled\": true, \"plan\": \"Enterprise (Annually)\",\"planType\": 11,\"seats\": 6,\"maxCollections\": null,\"usePolicies\": true,\"useSso\": true,\"useKeyConnector\": false,\"useGroups\": true,\"useEvents\": true,\"useDirectory\": true,\"useTotp\": true,\"use2fa\": true,\"useApi\": true,\"useResetPassword\": true,\"maxStorageGb\": 1,\"selfHost\": true,\"usersGetPremium\": true,\"version\": 8,\"issued\": \"2022-01-25T21:58:38.9454581Z\",\"refresh\": \"2022-01-28T14:26:31Z\",\"expires\": \"2022-01-28T14:26:31Z\",\"trial\": true,\"hash\": \"testvalue\",\"signature\": \"signature\"}";
}
