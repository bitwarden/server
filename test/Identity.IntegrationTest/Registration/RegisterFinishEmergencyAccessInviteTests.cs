using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.Accounts;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Tokens;
using Bit.IntegrationTestCommon.Factories;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Bit.Identity.IntegrationTest.Registration;

public class RegisterFinishEmergencyAccessInviteTests
{
    [Theory, BitAutoData]
    public async Task RegistrationWithEmailVerification_WithAcceptEmergencyAccessInviteToken_Succeeds(
     [StringLength(1000)] string masterPasswordHash, [StringLength(50)] string masterPasswordHint, string userSymmetricKey,
    KeysRequestModel userAsymmetricKeys, int kdfMemory, int kdfParallelism, EmergencyAccess emergencyAccess)
    {
        userAsymmetricKeys.AccountKeys = null;

        // Localize factory to just this test.
        var localFactory = new IdentityApplicationFactory();

        // Hardcoded, valid data
        var email = "jsnider+local79813655659549@bitwarden.com";
        var acceptEmergencyAccessInviteToken = "CfDJ8HFsgwUNr89EtnCal5H72cwjvdjWmBp3J0ry7KoG6zDFub-EeoA3cfLBXONq7thKq7QTBh6KJ--jU0Det7t3P9EXqxmEacxIlgFlBgtywIUho9N8nVQeNcltkQO9g0vj_ASshnn6fWK3zpqS6Z8JueVZ2TMtdks5uc7DjZurWFLX27Dpii-UusFD78Z5tCY-D79bkjHy43g1ULk2F2ZtwiJvp3C9QvXW1-12IEsyHHSxU-9RELe-_joo2iDIR-cvMmEfbEXK7uvuzNT2V0r22jalaAKFvd84Gza9Q0YSFn8z_nAJxVqEXsAVKdG8SRN5Wa3K2mdNoBMt20RrzNuuJhe6vzX0yP35HtC4e1YXXzWB";
        var acceptEmergencyAccessId = new Guid("8bc5e574-cef6-4ee7-b9ed-b1e90158c016");

        emergencyAccess.Id = acceptEmergencyAccessId;
        emergencyAccess.Email = email;

        var emergencyAccessInviteTokenable = new EmergencyAccessInviteTokenable(emergencyAccess, 10) { };

        localFactory.SubstituteService<IDataProtectorTokenFactory<EmergencyAccessInviteTokenable>>(dataProtectorTokenFactory =>
        {
            dataProtectorTokenFactory.TryUnprotect(Arg.Is(acceptEmergencyAccessInviteToken), out Arg.Any<EmergencyAccessInviteTokenable>())
                .Returns(callInfo =>
                {
                    callInfo[1] = emergencyAccessInviteTokenable;
                    return true;
                });
        });


        var registerFinishReqModel = new RegisterFinishRequestModel
        {
            Email = email,
            MasterPasswordHash = masterPasswordHash,
            MasterPasswordHint = masterPasswordHint,
            AcceptEmergencyAccessInviteToken = acceptEmergencyAccessInviteToken,
            AcceptEmergencyAccessId = acceptEmergencyAccessId,
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
