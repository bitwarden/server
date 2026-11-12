using System.ComponentModel.DataAnnotations;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Identity.IntegrationTest.Registration;

public class RegisterFinishSponsoredFamiliesTests
{
    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithOrgSponsoredFreeFamilyPlanInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism, Guid orgSponsorshipId)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid org sponsored free family plan invite token data
        var email = "jsnider+local10000008@bitwarden.com";
        var orgSponsoredFreeFamilyPlanToken = "BWOrganizationSponsorship_CfDJ8HFsgwUNr89EtnCal5H72cx11wdMdD5_FSNMJoXJKp9migo8ZXi2Qx8GOM2b8IccesQEvZxzX_VDvhaaFi1NZc7-5bdadsfaPiwvzy28qwaW5-iF72vncmixArxKt8_FrJCqvn-5Yh45DvUWeOUBl1fPPx6LB4lgf6DcFkFZaHKOxIEywkFWEX9IWsLAfBfhU9K7AYZ02kxLRgXDK_eH3SKY0luoyUbRLBJRq1J9WnAQNcPLx9GOywQDUGRNvQGYmrzpAdq8y3MgUby_XD2NBf4-Vfr_0DIYPlGVJz0Ab1CwKbQ5G9vTXrFbbHQni40GVgohTq6WeVwk-PBMW9kjBw2rHO8QzWUb4whn831y-dEC";

        var orgSponsorship = new OrganizationSponsorship
        {
            Id = orgSponsorshipId,
            PlanSponsorshipType = PlanSponsorshipType.FamiliesForEnterprise,
            OfferedToEmail = email
        };

        var orgSponsorshipOfferTokenable = new OrganizationSponsorshipOfferTokenable(orgSponsorship) { };

        localFactory.SubstituteService<IDataProtectorTokenFactory<OrganizationSponsorshipOfferTokenable>>(dataProtectorTokenFactory =>
        {
            dataProtectorTokenFactory.TryUnprotect(Arg.Is(orgSponsoredFreeFamilyPlanToken), out Arg.Any<OrganizationSponsorshipOfferTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = orgSponsorshipOfferTokenable;
                    return true;
                });
        });

        localFactory.SubstituteService<IOrganizationSponsorshipRepository>(organizationSponsorshipRepository =>
        {
            organizationSponsorshipRepository.GetByIdAsync(orgSponsorshipId)
                .Returns(orgSponsorship);
        });

        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            OrgSponsoredFreeFamilyPlanToken = orgSponsoredFreeFamilyPlanToken,
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
