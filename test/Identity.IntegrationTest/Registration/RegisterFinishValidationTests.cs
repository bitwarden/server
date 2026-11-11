using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Identity.IntegrationTest.Registration;

public class RegisterFinishValidationTests : IClassFixture<IdentityApplicationFactory>
{
    private readonly IdentityApplicationFactory _factory;

    public RegisterFinishValidationTests(IdentityApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory, BitAutoData]
    public async Task RegisterFinish_WithOpenOrgInviteAndOrgInviteToken_ReturnsBadRequest([Required] string name,
        [StringLength(1000), Required] string masterPasswordHash, [Required] string userSymmetricKey,
        [Required] KeysRequestModel userAsymmetricKeys, string orgInviteToken)
    {
        // DTO validation: OpenOrgInvite is only compatible with the EmailVerification token type.
        // Sending it alongside an OrgInviteToken must be rejected at the model-validation layer → 400.
        userAsymmetricKeys.AccountKeys = null;
        var email = $"test+register+dtoreject+{name}@email.com";

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = Guid.NewGuid(),
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            OpenOrgInvite = new OpenOrgInviteRequestModel
            {
                OrganizationId = Guid.NewGuid(),
                Code = Guid.NewGuid(),
            },
        };

        var finishCtx = await _factory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status400BadRequest, finishCtx.Response.StatusCode);
    }
}
