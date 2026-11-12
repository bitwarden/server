using System.ComponentModel.DataAnnotations;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Identity.IntegrationTest.Registration;

public class RegisterFinishDirectOrgInviteTests
{
    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOrgInviteToken_Succeeds(
         [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
        KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // To avoid having to call the API send org invite endpoint, I'm going to hardcode some valid org invite data:
        var email = "jsnider+local410@bitwarden.com";
        var orgInviteToken = "BwOrgUserInviteToken_CfDJ8HOzu6wr6nVLouuDxgOHsMwPcj9Guuip5k_XLD1bBGpwQS1f66c9kB6X4rvKGxNdywhgimzgvG9SgLwwJU70O8P879XyP94W6kSoT4N25a73kgW3nU3vl3fAtGSS52xdBjNU8o4sxmomRvhOZIQ0jwtVjdMC2IdybTbxwCZhvN0hKIFs265k6wFRSym1eu4NjjZ8pmnMneG0PlKnNZL93tDe8FMcqStJXoddIEgbA99VJp8z1LQmOMfEdoMEM7Zs8W5bZ34N4YEGu8XCrVau59kGtWQk7N4rPV5okzQbTpeoY_4FeywgLFGm-tDtTPEdSEBJkRjexANri7CGdg3dpnMifQc_bTmjZd32gOjw8N8v";
        var orgUserId = new Guid("5e45fbdc-a080-4a77-93ff-b19c0161e81e");

        var orgUser = new OrganizationUser { Id = orgUserId, Email = email, OrganizationId = Guid.NewGuid() };

        var orgInviteTokenable = new OrgUserInviteTokenable(orgUser)
        {
            ExpirationDate = DateTime.UtcNow.Add(TimeSpan.FromHours(5))
        };

        localFactory.SubstituteService<IDataProtectorTokenFactory<OrgUserInviteTokenable>>(orgInviteTokenDataProtectorFactory =>
        {
            orgInviteTokenDataProtectorFactory.TryUnprotect(Arg.Is(orgInviteToken), out Arg.Any<OrgUserInviteTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = orgInviteTokenable;
                    return true;
                });
        });

        localFactory.SubstituteService<IOrganizationUserRepository>(orgUserRepository =>
        {
            orgUserRepository.GetByIdAsync(orgUserId)
                .Returns(orgUser);
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            OrgInviteToken = orgInviteToken,
            OrganizationUserId = orgUserId,
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = KdfConstants.PBKDF2_ITERATIONS.Default,
            UserSymmetricKey = userSymmetricKey,
            UserAsymmetricKeys = userAsymmetricKeys,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism
        };

        var postRegisterFinishHttpContext = await localFactory.PostRegisterFinishAsync(registerFinishReqModel);

        Assert.Equal(StatusCodes.Status200OK, postRegisterFinishHttpContext.Response.StatusCode);

        var database = localFactory.GetDatabaseContext();
        var user = await database.Users
            .SingleAsync(u => u.Email == email);

        Assert.NotNull(user);

        // Assert user properties match the request model
        Assert.Equal(email, user.Email);
        Assert.NotEqual(masterPasswordHash, user.MasterPassword);  // We execute server side hashing
        Assert.NotNull(user.MasterPassword);
        Assert.Equal(masterPasswordHint, user.MasterPasswordHint);
        Assert.Equal(userSymmetricKey, user.Key);
        Assert.Equal(userAsymmetricKeys.EncryptedPrivateKey, user.PrivateKey);
        Assert.Equal(userAsymmetricKeys.PublicKey, user.PublicKey);
        Assert.Equal(KdfType.PBKDF2_SHA256, user.Kdf);
        Assert.Equal(KdfConstants.PBKDF2_ITERATIONS.Default, user.KdfIterations);
        Assert.Equal(kdfMemory, user.KdfMemory);
        Assert.Equal(kdfParallelism, user.KdfParallelism);
    }
}
